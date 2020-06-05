using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XFCharReplacer.Format;
using System.Text.Json;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace XFCharReplacer
{
    class Program
    {
        static void Main(string[] args)
        {
            if(args.Length < 1)
            {
                Console.WriteLine("XFCharReplacer <XF Path> <Chars JSON Path> <Output XF Path> <table JSON Path>");
            }
            var fileStream = File.Open(args[0], FileMode.Open);
            var xf = new XF(fileStream);
            var chars = JsonDocument.Parse(File.Open(args[1], FileMode.Open));
            var exists = new Dictionary<char, XF.CharacterMap>();
            var notExists = new List<char>();
            foreach (JsonElement c in chars.RootElement.EnumerateArray())
            {
                var charStr = c.GetString().Trim('\n', '\r');
                var character = charStr.ToCharArray();
                if (character.Length > 0 && xf.dicGlyphLarge.ContainsKey(character[0]))
                {
                    exists[character[0]] = xf.dicGlyphLarge[character[0]];
                } else if (character.Length > 0)
                {
                    notExists.Add(character[0]);
                }
            }
            Console.WriteLine("Found " + exists.Count + " exist chars, " + notExists.Count + " non-exist chars.");
            Console.WriteLine("Replacing unused chars to non-exist chars. This may take a while...");
            notExists.Sort();
            var index = 0;
            var tbl = new Dictionary<string, char>();
            var editedDicGlyphLarge = new Dictionary<char, XF.CharacterMap>();
            var fontSize = 14;
            foreach (KeyValuePair<char, XF.CharacterMap> pair in xf.dicGlyphLarge)
            {
                if(index >= notExists.Count)
                {
                    break;
                }
                if (((int) pair.Key) >= 0x4E00 && !exists.ContainsKey(pair.Value.code_point) && xf.lstCharSizeInfoLarge[pair.Value.code_point].char_width >= fontSize && xf.lstCharSizeInfoLarge[pair.Value.code_point].char_height >= fontSize)
                {
                    tbl.Add(pair.Key + "", notExists[index]);
                    // TO-DO Replacing unused chars to non-exists chars.
                    RectangleF rectf = new RectangleF(pair.Value.ImageOffsetX, pair.Value.ImageOffsetY, fontSize, fontSize);
                    Bitmap image = pair.Value.ColorChannel == 0 ? xf.image_0 : pair.Value.ColorChannel == 1 ? xf.image_1 : xf.image_2;
                    for(int xP=pair.Value.ImageOffsetX;xP< pair.Value.ImageOffsetX+xf.lstCharSizeInfoLarge[pair.Value.code_point].char_width;xP++)
                    {
                        for(int yP=pair.Value.ImageOffsetY;yP < pair.Value.ImageOffsetY+xf.lstCharSizeInfoLarge[pair.Value.code_point].char_height;yP++)
                        {
                            image.SetPixel(xP, yP, Color.Transparent);
                        }
                    }
                    Graphics g = Graphics.FromImage(image);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    editedDicGlyphLarge.Add(pair.Key, new XF.CharacterMap
                    {
                        char_size = (ushort)((149 & 0x3FF) | ((Convert.ToUInt16(fontSize) & 0x3F) << 10)),
                        code_point = pair.Value.code_point,
                        image_offset = pair.Value.image_offset
                    });
                    xf.lstCharSizeInfoLarge[pair.Value.code_point].char_height = (byte) fontSize;
                    xf.lstCharSizeInfoLarge[pair.Value.code_point].char_width = (byte) fontSize;
                    xf.lstCharSizeInfoLarge[pair.Value.code_point].offset_x = 0;
                    xf.lstCharSizeInfoLarge[pair.Value.code_point].offset_y = 0;
                    g.DrawString(notExists[index] + "", new Font("NanumSquare", fontSize, FontStyle.Regular, GraphicsUnit.Pixel), Brushes.White, rectf);
                    g.Flush();
                    if (pair.Value.ColorChannel == 0)
                    {
                        xf.image_0 = image;
                    } else if(pair.Value.ColorChannel == 1)
                    {
                        xf.image_1 = image;
                    } else
                    {
                        xf.image_2 = image;
                    }
                    index++;
                }
            }
            if(index < notExists.Count)
            {
                Console.WriteLine("[WARNING] Some characters are not replaced correctly, Please change font size.");
            }
            foreach (KeyValuePair<char, XF.CharacterMap> newPair in editedDicGlyphLarge)
            {
                xf.dicGlyphLarge[newPair.Key] = newPair.Value;
            }
            var newFile = File.Create(args[2]);
            xf.Save(newFile);
            Console.WriteLine("Char Replaced. Generating Table...");
            var fileWriter = new StreamWriter(args[3], false, Encoding.UTF8, 8192);
            var options = new JsonSerializerOptions();
            options.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
            var tblSerialized = JsonSerializer.Serialize(tbl, options);
            fileWriter.WriteLine(tblSerialized);
            fileWriter.Close();
            Console.WriteLine("Table Generated.");
        }
    }
}
