using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 한글 폰트 서브셋: 게임 코드에 나오는 글자 + ASCII만 남긴 작은 TTF를 만든다 (WebGL 빌드 용량 절약).
// 원본(가변 폰트, 약 10MB)은 빌드에 안 들어가는 FoxGame/FontSource/에 두고, 결과만 Resources에 둔다.
// 가변 정보(fvar/gvar 등)와 조판 테이블(GSUB/GPOS 등)은 빼고 기본 굵기 윤곽(glyf)만 남긴다.
// 메뉴: Tools > Fox > 한글 폰트 서브셋 (WebGL 빌드 때도 자동 실행)
public static class FontSubsetter
{
    const string SourcePath = "FontSource/NotoSansKR-VF.ttf";          // 프로젝트 폴더 기준 (Assets 밖)
    const string OutputPath = "Assets/Resources/Fonts/NotoSansKR.ttf"; // ResultScreen이 Resources.Load로 쓴다
    static readonly string[] ScanDirs = { "Assets/Scripts", "Assets/Editor" };

    [MenuItem("Tools/Fox/한글 폰트 서브셋")]
    public static void RunMenu() => Run();

    // 반환: 새로 썼으면 true (내용이 같으면 파일을 건드리지 않음)
    public static bool Run()
    {
        if (!File.Exists(SourcePath))
        {
            Debug.LogError($"[FontSubsetter] 원본 폰트가 없습니다: {Path.GetFullPath(SourcePath)}");
            return false;
        }

        var chars = CollectCharacters();
        byte[] src = File.ReadAllBytes(SourcePath);
        byte[] subset = Subset(src, chars, out int glyphs, out var missing);

        bool changed = !File.Exists(OutputPath) || !File.ReadAllBytes(OutputPath).SequenceEqual(subset);
        if (changed)
        {
            File.WriteAllBytes(OutputPath, subset);
            AssetDatabase.ImportAsset(OutputPath, ImportAssetOptions.ForceUpdate);
        }

        string miss = missing.Count == 0 ? "없음" : string.Concat(missing.Select(c => char.ConvertFromUtf32(c)));
        string line = $"[FontSubsetter] {(changed ? "새로 만듦" : "변경 없음")} | 글자 {chars.Count}개, 글리프 {glyphs}개 | " +
                      $"{src.Length / 1024f / 1024f:F1}MB → {subset.Length / 1024f:F0}KB | 원본에 없는 글자: {miss}";
        if (missing.Count == 0) Debug.Log(line); else Debug.LogWarning(line);
        return changed;
    }

    // 게임 코드의 문자열에 나올 수 있는 글자: ASCII 전부 + 스크립트 파일에 나오는 모든 비ASCII 글자
    // (에디터 셋업이 씬에 넣는 문구도 Assets/Editor에 있으므로 함께 훑는다)
    public static SortedSet<int> CollectCharacters()
    {
        var set = new SortedSet<int>();
        for (int c = 0x20; c <= 0x7E; c++) set.Add(c);
        foreach (var dir in ScanDirs)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(file, Encoding.UTF8);
                for (int i = 0; i < text.Length; i++)
                {
                    int cp = char.ConvertToUtf32(text, i);
                    if (char.IsHighSurrogate(text[i])) i++;
                    if (cp > 0x7E && !char.IsControl((char)Math.Min(cp, 0xFFFF))) set.Add(cp);
                }
            }
        }
        return set;
    }

    // ── TrueType 서브셋 ──────────────────────────────────────────────
    static byte[] Subset(byte[] font, SortedSet<int> chars, out int glyphCount, out List<int> missing)
    {
        var tables = ReadTableDirectory(font);
        byte[] T(string tag) => tables.TryGetValue(tag, out var t) ? t : throw new Exception($"'{tag}' 테이블 없음 (TrueType glyf 폰트만 지원)");

        byte[] head = (byte[])T("head").Clone();
        byte[] hhea = (byte[])T("hhea").Clone();
        byte[] maxp = (byte[])T("maxp").Clone();
        byte[] hmtx = T("hmtx"), loca = T("loca"), glyf = T("glyf"), cmap = T("cmap"), post = T("post");

        int numGlyphs = U16(maxp, 4);
        int numHMetrics = U16(hhea, 34);
        bool longLoca = U16(head, 50) == 1;
        int GlyphStart(int g) => longLoca ? (int)U32(loca, g * 4) : U16(loca, g * 2) * 2;
        int GlyphEnd(int g) => GlyphStart(g + 1);

        // 1) 글자 → 원래 글리프 번호
        var cmapOld = ReadCmap(cmap);
        missing = new List<int>();
        var charToOld = new SortedDictionary<int, int>();
        foreach (int c in chars)
        {
            if (cmapOld.TryGetValue(c, out int g) && g != 0) charToOld[c] = g;
            else if (c > 0x7E) missing.Add(c);
        }

        // 2) 남길 글리프: .notdef + 쓰는 글자 + 합성 글리프가 참조하는 부품
        var keep = new SortedSet<int> { 0 };
        var stack = new Stack<int>(charToOld.Values);
        while (stack.Count > 0)
        {
            int g = stack.Pop();
            if (!keep.Add(g)) continue;
            foreach (int comp in Components(glyf, GlyphStart(g), GlyphEnd(g))) stack.Push(comp);
        }
        var oldToNew = new Dictionary<int, int>();
        foreach (int g in keep) oldToNew[g] = oldToNew.Count;
        glyphCount = keep.Count;

        // 3) glyf + loca (long 형식)
        var glyfOut = new MemoryStream();
        var locaOut = new List<uint>();
        foreach (int g in keep)
        {
            locaOut.Add((uint)glyfOut.Length);
            int s = GlyphStart(g), e = GlyphEnd(g);
            if (e > s)
            {
                byte[] data = new byte[e - s];
                Buffer.BlockCopy(glyf, s, data, 0, data.Length);
                RemapComponents(data, oldToNew);
                glyfOut.Write(data, 0, data.Length);
                while (glyfOut.Length % 4 != 0) glyfOut.WriteByte(0);
            }
        }
        locaOut.Add((uint)glyfOut.Length);
        var locaBytes = new byte[locaOut.Count * 4];
        for (int i = 0; i < locaOut.Count; i++) W32(locaBytes, i * 4, locaOut[i]);

        // 4) hmtx: 모든 글리프를 (advance, lsb) 쌍으로
        var hmtxOut = new byte[keep.Count * 4];
        int k = 0;
        foreach (int g in keep)
        {
            int adv = g < numHMetrics ? U16(hmtx, g * 4) : U16(hmtx, (numHMetrics - 1) * 4);
            int lsb = g < numHMetrics ? U16(hmtx, g * 4 + 2) : U16(hmtx, numHMetrics * 4 + (g - numHMetrics) * 2);
            W16(hmtxOut, k * 4, adv);
            W16(hmtxOut, k * 4 + 2, lsb);
            k++;
        }

        // 5) 헤더 테이블 갱신
        W16(maxp, 4, keep.Count);
        W16(hhea, 34, keep.Count);
        W16(head, 50, 1);           // indexToLocFormat = long
        W32(head, 8, 0);            // checkSumAdjustment는 마지막에 계산
        byte[] postOut = new byte[32];
        Buffer.BlockCopy(post, 0, postOut, 0, 32);
        W32(postOut, 0, 0x00030000); // 글리프 이름 없음

        var cmapNew = new SortedDictionary<int, int>();
        foreach (var kv in charToOld) cmapNew[kv.Key] = oldToNew[kv.Value];

        var outTables = new SortedDictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["head"] = head, ["hhea"] = hhea, ["maxp"] = maxp, ["hmtx"] = hmtxOut,
            ["loca"] = locaBytes, ["glyf"] = glyfOut.ToArray(), ["cmap"] = BuildCmap(cmapNew), ["post"] = postOut,
        };
        foreach (var tag in new[] { "OS/2", "name", "gasp", "cvt ", "fpgm", "prep" })
            if (tables.TryGetValue(tag, out var t)) outTables[tag] = t;

        return WriteFont(outTables);
    }

    static Dictionary<string, byte[]> ReadTableDirectory(byte[] f)
    {
        uint version = U32(f, 0);
        if (version != 0x00010000 && version != 0x74727565) throw new Exception("TrueType 폰트가 아닙니다");
        int n = U16(f, 4);
        var d = new Dictionary<string, byte[]>();
        for (int i = 0; i < n; i++)
        {
            int r = 12 + i * 16;
            string tag = Encoding.ASCII.GetString(f, r, 4);
            int off = (int)U32(f, r + 8), len = (int)U32(f, r + 12);
            var t = new byte[len];
            Buffer.BlockCopy(f, off, t, 0, len);
            d[tag] = t;
        }
        return d;
    }

    // cmap의 유니코드 서브테이블(형식 12 우선, 없으면 4)을 읽는다
    static Dictionary<int, int> ReadCmap(byte[] cmap)
    {
        int n = U16(cmap, 2);
        int best = -1, bestFormat = 0;
        for (int i = 0; i < n; i++)
        {
            int pid = U16(cmap, 4 + i * 8), eid = U16(cmap, 6 + i * 8);
            int off = (int)U32(cmap, 8 + i * 8);
            int fmt = U16(cmap, off);
            bool unicode = pid == 0 || (pid == 3 && (eid == 1 || eid == 10));
            if (!unicode) continue;
            if (fmt == 12 || (fmt == 4 && bestFormat != 12)) { best = off; bestFormat = fmt; }
        }
        if (best < 0) throw new Exception("유니코드 cmap이 없습니다");

        var map = new Dictionary<int, int>();
        if (bestFormat == 12)
        {
            int groups = (int)U32(cmap, best + 12);
            for (int i = 0; i < groups; i++)
            {
                int p = best + 16 + i * 12;
                int start = (int)U32(cmap, p), end = (int)U32(cmap, p + 4), gid = (int)U32(cmap, p + 8);
                for (int c = start; c <= end; c++) map[c] = gid + (c - start);
            }
        }
        else
        {
            int segX2 = U16(cmap, best + 6);
            int ends = best + 14, starts = ends + segX2 + 2, deltas = starts + segX2, ranges = deltas + segX2;
            for (int s = 0; s < segX2 / 2; s++)
            {
                int end = U16(cmap, ends + s * 2), start = U16(cmap, starts + s * 2);
                int delta = (short)U16(cmap, deltas + s * 2), ro = U16(cmap, ranges + s * 2);
                for (int c = start; c <= end && c != 0xFFFF; c++)
                {
                    int g;
                    if (ro == 0) g = (c + delta) & 0xFFFF;
                    else
                    {
                        int gp = ranges + s * 2 + ro + (c - start) * 2;
                        g = U16(cmap, gp);
                        if (g != 0) g = (g + delta) & 0xFFFF;
                    }
                    map[c] = g;
                }
            }
        }
        return map;
    }

    // 합성 글리프(numberOfContours < 0)의 부품 글리프 번호
    static IEnumerable<int> Components(byte[] glyf, int s, int e)
    {
        if (e - s < 10 || (short)U16(glyf, s) >= 0) yield break;
        int p = s + 10;
        while (true)
        {
            int flags = U16(glyf, p);
            yield return U16(glyf, p + 2);
            p += 4 + ComponentArgSize(flags);
            if ((flags & 0x0020) == 0) break; // MORE_COMPONENTS
        }
    }

    static void RemapComponents(byte[] g, Dictionary<int, int> oldToNew)
    {
        if (g.Length < 10 || (short)U16(g, 0) >= 0) return;
        int p = 10;
        while (true)
        {
            int flags = U16(g, p);
            W16(g, p + 2, oldToNew[U16(g, p + 2)]);
            p += 4 + ComponentArgSize(flags);
            if ((flags & 0x0020) == 0) break;
        }
    }

    static int ComponentArgSize(int flags)
    {
        int size = (flags & 0x0001) != 0 ? 4 : 2;       // ARG_1_AND_2_ARE_WORDS
        if ((flags & 0x0008) != 0) size += 2;            // WE_HAVE_A_SCALE
        else if ((flags & 0x0040) != 0) size += 4;       // WE_HAVE_AN_X_AND_Y_SCALE
        else if ((flags & 0x0080) != 0) size += 8;       // WE_HAVE_A_TWO_BY_TWO
        return size;
    }

    // cmap 형식 4 (Windows 유니코드 BMP): 글자마다 한 구간, idDelta로 새 글리프 번호 지정
    static byte[] BuildCmap(SortedDictionary<int, int> map)
    {
        var codes = map.Keys.Where(c => c < 0xFFFF).ToList();
        int segs = codes.Count + 1;
        int subLen = 16 + segs * 8;
        var b = new byte[4 + 8 + subLen];
        W16(b, 0, 0); W16(b, 2, 1);                 // version, numTables
        W16(b, 4, 3); W16(b, 6, 1); W32(b, 8, 12);  // platform 3, encoding 1, offset
        int o = 12;
        int log = FloorLog2(segs);
        int searchRange = 2 << log;
        W16(b, o, 4); W16(b, o + 2, subLen); W16(b, o + 4, 0);
        W16(b, o + 6, segs * 2); W16(b, o + 8, searchRange);
        W16(b, o + 10, log); W16(b, o + 12, segs * 2 - searchRange);
        int ends = o + 14, starts = ends + segs * 2 + 2, deltas = starts + segs * 2, ranges = deltas + segs * 2;
        for (int i = 0; i < codes.Count; i++)
        {
            int c = codes[i];
            W16(b, ends + i * 2, c);
            W16(b, starts + i * 2, c);
            W16(b, deltas + i * 2, (map[c] - c) & 0xFFFF);
            W16(b, ranges + i * 2, 0);
        }
        int last = codes.Count;
        W16(b, ends + last * 2, 0xFFFF); W16(b, starts + last * 2, 0xFFFF); W16(b, deltas + last * 2, 1); W16(b, ranges + last * 2, 0);
        return b;
    }

    static byte[] WriteFont(SortedDictionary<string, byte[]> tables)
    {
        int n = tables.Count;
        int log = FloorLog2(n);
        int pow = 1 << log;
        var ms = new MemoryStream();
        var header = new byte[12 + n * 16];
        W32(header, 0, 0x00010000);
        W16(header, 4, n); W16(header, 6, pow * 16); W16(header, 8, log); W16(header, 10, n * 16 - pow * 16);
        ms.Write(header, 0, header.Length);

        int i = 0, headOffset = 0;
        foreach (var kv in tables)
        {
            int offset = (int)ms.Length;
            if (kv.Key == "head") headOffset = offset;
            ms.Write(kv.Value, 0, kv.Value.Length);
            while (ms.Length % 4 != 0) ms.WriteByte(0);
            int r = 12 + i * 16;
            Encoding.ASCII.GetBytes(kv.Key, 0, 4, header, r);
            W32(header, r + 4, Checksum(kv.Value));
            W32(header, r + 8, (uint)offset);
            W32(header, r + 12, (uint)kv.Value.Length);
            i++;
        }
        byte[] font = ms.ToArray();
        Buffer.BlockCopy(header, 0, font, 0, header.Length);
        W32(font, headOffset + 8, 0xB1B0AFBA - Checksum(font));
        return font;
    }

    static uint Checksum(byte[] d)
    {
        uint sum = 0;
        for (int i = 0; i < d.Length; i += 4)
        {
            uint v = 0;
            for (int j = 0; j < 4; j++) v = (v << 8) | (i + j < d.Length ? d[i + j] : 0u);
            sum += v;
        }
        return sum;
    }

    static int FloorLog2(int v)
    {
        int log = 0;
        while ((2 << log) <= v) log++;
        return log;
    }

    static int U16(byte[] b, int o) => (b[o] << 8) | b[o + 1];
    static uint U32(byte[] b, int o) => (uint)((b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3]);
    static void W16(byte[] b, int o, int v) { b[o] = (byte)(v >> 8); b[o + 1] = (byte)v; }
    static void W32(byte[] b, int o, uint v) { b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v; }
}
