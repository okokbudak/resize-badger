// Uygulama simgesini (src/app.ico) çizer. Kullanım: MakeIcon.exe <app.ico yolu> [önizleme.png]
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

class MakeIcon
{
    static Bitmap Render(int s)
    {
        Bitmap bmp = new Bitmap(s, s, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);
            float pad = s * 0.04f, size = s - 2 * pad, r = s * 0.22f;
            using (GraphicsPath tile = new GraphicsPath())
            {
                tile.AddArc(pad, pad, r * 2, r * 2, 180, 90); tile.AddArc(pad + size - r * 2, pad, r * 2, r * 2, 270, 90);
                tile.AddArc(pad + size - r * 2, pad + size - r * 2, r * 2, r * 2, 0, 90); tile.AddArc(pad, pad + size - r * 2, r * 2, r * 2, 90, 90);
                tile.CloseFigure();
                using (LinearGradientBrush b = new LinearGradientBrush(new PointF(0, 0), new PointF(s, s), Color.FromArgb(34, 197, 140), Color.FromArgb(6, 104, 82)))
                    g.FillPath(b, tile);
            }
            // pencere çerçevesi
            float f0 = s * 0.22f, f1 = s * 0.78f;
            using (Pen fp = new Pen(Color.FromArgb(120, 255, 255, 255), Math.Max(1f, s * 0.05f)))
                g.DrawRectangle(fp, f0, f0 + s * 0.02f, f1 - f0, f1 - f0 - s * 0.04f);
            // çapraz genişletme okları
            float c = s / 2f, d = s * 0.30f, head = s * 0.16f, hw = s * 0.11f, lw = Math.Max(1.5f, s * 0.07f);
            using (Pen p = new Pen(Color.White, lw))
            using (SolidBrush wb = new SolidBrush(Color.White))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                foreach (int sign in new int[] { 1, -1 })
                {
                    PointF tip = new PointF(c + sign * d, c - sign * d);
                    PointF from = new PointF(c - sign * d * 0.35f, c + sign * d * 0.35f);
                    g.DrawLine(p, from, new PointF(tip.X - sign * head * 0.55f, tip.Y + sign * head * 0.55f));
                    // ok başı: köşeyi gösteren üçgen
                    PointF a = new PointF(tip.X - sign * head, tip.Y), bpt = new PointF(tip.X, tip.Y + sign * head);
                    g.FillPolygon(wb, new PointF[] { new PointF(tip.X + sign * hw * 0.35f, tip.Y - sign * hw * 0.35f), a, bpt });
                }
            }
        }
        return bmp;
    }

    static void Main(string[] a)
    {
        int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };
        List<byte[]> pngs = new List<byte[]>();
        foreach (int s in sizes)
            using (Bitmap bmp = Render(s))
            using (MemoryStream ms = new MemoryStream()) { bmp.Save(ms, ImageFormat.Png); pngs.Add(ms.ToArray()); }
        using (FileStream fs = new FileStream(a[0], FileMode.Create))
        using (BinaryWriter w = new BinaryWriter(fs))
        {
            w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i])); w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                w.Write((byte)0); w.Write((byte)0); w.Write((short)1); w.Write((short)32);
                w.Write(pngs[i].Length); w.Write(offset); offset += pngs[i].Length;
            }
            foreach (byte[] b in pngs) w.Write(b);
        }
        if (a.Length > 1) using (Bitmap big = Render(256)) big.Save(a[1], ImageFormat.Png);
        Console.WriteLine("icon yazildi: " + a[0]);
    }
}
