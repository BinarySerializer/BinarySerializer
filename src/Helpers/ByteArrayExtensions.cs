using System.Text;

namespace BinarySerializer
{
    public static class ByteArrayExtensions
    {
        public static string ToHexString(this byte[] bytes, int? align = null, string newLinePrefix = null, int? maxLines = null)
        {
            StringBuilder Result = new StringBuilder(bytes.Length * 2);
            const string HexAlphabet = "0123456789ABCDEF";
            int curLine = 0;
            for (int i = 0; i < bytes.Length; i++)
            {
                if (i > 0 && align.HasValue && i % align == 0)
                {
                    curLine++;
                    if (maxLines.HasValue && curLine >= maxLines.Value)
                    {
                        Result.Append("...");
                        return Result.ToString();
                    }
                    Result.Append("\n" + newLinePrefix ?? "");
                }
                byte B = bytes[i];
                Result.Append(HexAlphabet[(int)(B >> 4)]);
                Result.Append(HexAlphabet[(int)(B & 0xF)]);

                if (i < bytes.Length - 1) 
                    Result.Append(' ');
            }

            return Result.ToString();
        }
    }
}