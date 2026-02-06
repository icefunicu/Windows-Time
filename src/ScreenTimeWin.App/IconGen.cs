using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace IconGenerator
{
    public class Program
    {
        public static void MainGen()
        {
            GenerateIcon("app.ico");
            Console.WriteLine("Icon generated: app.ico");
        }

        public static void GenerateIcon(string filePath)
        {
            // Create a 256x256 bitmap
            using var bitmap = new Bitmap(256, 256);
            using var g = Graphics.FromImage(bitmap);
            
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            // 1. Background (Rounded Rect, iOS Style)
            var rect = new Rectangle(10, 10, 236, 236);
            using var roundedPath = GetRoundedRect(rect, 50);
            
            // Gradient Brush
            using var brush = new LinearGradientBrush(rect, Color.FromArgb(0, 122, 255), Color.FromArgb(0, 99, 200), 45f);
            g.FillPath(brush, roundedPath);

            // 2. Clock Face
            var center = new Point(128, 128);
            int radius = 80;
            using var whitePen = new Pen(Color.White, 12);
            g.DrawEllipse(whitePen, center.X - radius, center.Y - radius, radius * 2, radius * 2);

            // 3. Hands
            // Hour Hand
            using var hourPen = new Pen(Color.White, 16) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(hourPen, center.X, center.Y, center.X + 30, center.Y - 30); // 1:30 approx
            
            // Minute Hand
            using var minutePen = new Pen(Color.White, 12) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(minutePen, center.X, center.Y, center.X, center.Y - 60); // 12:00

            // Center Dot
            int dotR = 10;
            g.FillEllipse(Brushes.White, center.X - dotR, center.Y - dotR, dotR * 2, dotR * 2);

            // Save as ICO
            // Simple ICO header for 1 image
            using var stream = new FileStream(filePath, FileMode.Create);
            using var writer = new BinaryWriter(stream);

            // ICO Header
            writer.Write((short)0); // Reserved
            writer.Write((short)1); // Type (1=Icon)
            writer.Write((short)1); // Count (1 image)

            // Image Entry
            writer.Write((byte)0); // Width (0 = 256)
            writer.Write((byte)0); // Height (0 = 256)
            writer.Write((byte)0); // ColorCount
            writer.Write((byte)0); // Reserved
            writer.Write((short)1); // Planes
            writer.Write((short)32); // BitCount
            
            // Convert Bitmap to PNG for the image data (Vista+ supports PNG in ICO)
            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Png);
            byte[] pngData = ms.ToArray();
            
            writer.Write((int)pngData.Length); // SizeInBytes
            writer.Write((int)(6 + 16)); // Offset (Header 6 + Entry 16)

            // Image Data
            writer.Write(pngData);
        }

        private static GraphicsPath GetRoundedRect(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            Size size = new Size(diameter, diameter);
            Rectangle arc = new Rectangle(bounds.Location, size);
            GraphicsPath path = new GraphicsPath();

            if (radius == 0)
            {
                path.AddRectangle(bounds);
                return path;
            }

            // Top left arc  
            path.AddArc(arc, 180, 90);

            // Top right arc  
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);

            // Bottom right arc  
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);

            // Bottom left arc 
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }
    }
}