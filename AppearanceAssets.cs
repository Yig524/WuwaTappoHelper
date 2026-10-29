namespace TappoHelper.Art {
    using System;using System.Drawing;using System.Drawing.Drawing2D;using System.Drawing.Imaging;using System.IO;
    public static class AssetBuilder {
        static GraphicsPath Round(float x,float y,float w,float h,float r){GraphicsPath p=new GraphicsPath();p.AddArc(x,y,r,r,180,90);p.AddArc(x+w-r,y,r,r,270,90);p.AddArc(x+w-r,y+h-r,r,r,0,90);p.AddArc(x,y+h-r,r,r,90,90);p.CloseFigure();return p;}
        static Bitmap Paint(int size){
            Bitmap bitmap=new Bitmap(size,size,PixelFormat.Format32bppArgb);
            using(Graphics g=Graphics.FromImage(bitmap)){
                g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Color.Transparent);g.ScaleTransform(size/256f,size/256f);
                using(GraphicsPath bg=Round(8,8,240,240,62))using(LinearGradientBrush brush=new LinearGradientBrush(new Point(30,20),new Point(228,242),Color.FromArgb(39,99,81),Color.FromArgb(17,56,47)))g.FillPath(brush,bg);
                using(GraphicsPath cat=new GraphicsPath()){
                    cat.StartFigure();cat.AddLine(62,121,60,60);cat.AddBezier(60,60,59,49,72,51,78,57);cat.AddLine(78,57,106,79);cat.AddBezier(106,79,119,74,137,74,150,79);cat.AddLine(150,79,178,57);cat.AddBezier(178,57,184,51,197,49,196,60);cat.AddLine(196,60,194,121);cat.AddBezier(194,121,213,173,184,202,128,202);cat.AddBezier(128,202,72,202,43,173,62,121);cat.CloseFigure();
                    using(SolidBrush b=new SolidBrush(Color.FromArgb(197,239,214)))g.FillPath(b,cat);
                }
                using(SolidBrush ink=new SolidBrush(Color.FromArgb(24,72,58))){g.FillEllipse(ink,93,125,13,17);g.FillEllipse(ink,150,125,13,17);g.FillPolygon(ink,new PointF[]{new PointF(122,150),new PointF(134,150),new PointF(128,157)});}
                using(Pen smile=new Pen(Color.FromArgb(24,72,58),4)){smile.StartCap=smile.EndCap=LineCap.Round;g.DrawArc(smile,115,151,14,15,0,140);g.DrawArc(smile,127,151,14,15,40,140);}
                using(SolidBrush sparkle=new SolidBrush(Color.FromArgb(245,204,117)))g.FillPolygon(sparkle,new PointF[]{new PointF(218,45),new PointF(223,59),new PointF(237,64),new PointF(223,69),new PointF(218,83),new PointF(213,69),new PointF(199,64),new PointF(213,59)});
                if(size>=40){using(GraphicsPath box=Round(172,177,54,44,13))using(SolidBrush b=new SolidBrush(Color.FromArgb(244,200,107)))g.FillPath(b,box);using(Pen p=new Pen(Color.FromArgb(94,69,31),4)){g.DrawLine(p,181,192,217,192);g.DrawRectangle(p,196,190,6,11);}}
            }return bitmap;
        }
        public static void Save(string directory){
            Directory.CreateDirectory(directory);int[] sizes=new int[]{16,20,24,32,40,48,64,128,256};byte[][] images=new byte[sizes.Length][];
            for(int i=0;i<sizes.Length;i++)using(Bitmap bitmap=Paint(sizes[i]))using(MemoryStream stream=new MemoryStream()){bitmap.Save(stream,ImageFormat.Png);images[i]=stream.ToArray();if(sizes[i]==128)bitmap.Save(Path.Combine(directory,"app-icon.png"),ImageFormat.Png);}
            using(BinaryWriter output=new BinaryWriter(File.Create(Path.Combine(directory,"app.ico")))){
                output.Write((ushort)0);output.Write((ushort)1);output.Write((ushort)sizes.Length);int offset=6+16*sizes.Length;
                for(int i=0;i<sizes.Length;i++){output.Write((byte)(sizes[i]==256?0:sizes[i]));output.Write((byte)(sizes[i]==256?0:sizes[i]));output.Write((byte)0);output.Write((byte)0);output.Write((ushort)1);output.Write((ushort)32);output.Write(images[i].Length);output.Write(offset);offset+=images[i].Length;}
                foreach(byte[] data in images)output.Write(data);
            }
        }
    }
}
