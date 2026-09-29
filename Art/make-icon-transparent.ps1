# Make the white area outside the rounded icon transparent (Codex image generation returns an opaque RGB PNG).
# Usage (repo root): powershell -ExecutionPolicy Bypass -File Art/make-icon-transparent.ps1 [-Src Art/app_icon.png] [-Dst Art/app_icon_transparent.png]
# Flood-fills near-white pixels from the 4 corners -> alpha 0; anti-aliased edge pixels get partial alpha in the tile color.
# After running: copy the result to FoxGame/Assets/UI/app_icon.png (Unity exe icon) - Store/make-msix.ps1 reads it from Art/.
param(
    [string]$Src = "Art/app_icon.png",
    [string]$Dst = "Art/app_icon_transparent.png"
)
$repo = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
function P($rel) { if ([System.IO.Path]::IsPathRooted($rel)) { $rel } else { Join-Path $repo $rel } }

Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System; using System.Collections.Generic; using System.Drawing; using System.Drawing.Imaging; using System.Runtime.InteropServices;
public static class IconAlpha {
  public static void Run(string src, string dst) {
    using (var inBmp = new Bitmap(src)) {
      int w = inBmp.Width, h = inBmp.Height;
      var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
      using (var g = Graphics.FromImage(bmp)) g.DrawImage(inBmp, 0, 0, w, h);
      var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
      var px = new byte[w * h * 4]; Marshal.Copy(data.Scan0, px, 0, px.Length);
      Func<int,int> minc = i => Math.Min(px[i*4+2], Math.Min(px[i*4+1], px[i*4]));
      var bg = new bool[w * h]; var q = new Queue<int>();
      foreach (var s in new[]{0, w-1, (h-1)*w, h*w-1}) { if (minc(s) > 235) { bg[s] = true; q.Enqueue(s); } }
      while (q.Count > 0) { int i = q.Dequeue(); int x = i % w, y = i / w;
        foreach (var n in new[]{ x>0?i-1:-1, x<w-1?i+1:-1, y>0?i-w:-1, y<h-1?i+w:-1 }) {
          if (n < 0 || bg[n]) continue; if (minc(n) > 235) { bg[n] = true; q.Enqueue(n); } } }
      int refI = (h/2)*w + w/20; byte rb = px[refI*4], rg = px[refI*4+1], rr = px[refI*4+2];
      int minRef = Math.Min(rr, Math.Min(rg, rb));
      for (int i = 0; i < w*h; i++) {
        if (bg[i]) { px[i*4+3] = 0; continue; }
        int x = i % w, y = i / w; bool edge = false;
        for (int dy=-2; dy<=2 && !edge; dy++) for (int dx=-2; dx<=2 && !edge; dx++) { int xx=x+dx, yy=y+dy; if (xx>=0&&yy>=0&&xx<w&&yy<h&&bg[yy*w+xx]) edge=true; }
        if (!edge) continue;
        double a = (255.0 - minc(i)) / Math.Max(1, 255 - minRef); a = Math.Max(0, Math.Min(1, a));
        px[i*4] = rb; px[i*4+1] = rg; px[i*4+2] = rr; px[i*4+3] = (byte)(a*255);
      }
      Marshal.Copy(px, 0, data.Scan0, px.Length); bmp.UnlockBits(data); bmp.Save(dst, ImageFormat.Png); bmp.Dispose();
    }
  }
}
'@
[IconAlpha]::Run((P $Src), (P $Dst))
Write-Host "Saved: $Dst"
