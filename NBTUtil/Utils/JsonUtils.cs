
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace NBTUtil.Utils
{
    class JsonUtils
    {
        public static bool saveListAsJsonFile(List<Vector3> list, string filePath)
        {
            if (!File.Exists(filePath))
                File.Create(filePath).Close();

            StreamWriter jsonWriter = File.CreateText(filePath);
            jsonWriter.WriteLine(toJsonString(list));

            jsonWriter.Close();

            return true;
        }

        public static string toJsonString(List<Vector3> list)
        {
            StringBuilder jsonBuilder = new StringBuilder();
            jsonBuilder.Append('[');

            string vectorString;
            for (int i = 0; i < list.Count; i++)
            {
                vectorString = toJsonString(list[i]);

                jsonBuilder.Append(vectorString);

                if (i + 1 < list.Count)
                    jsonBuilder.Append(',');
            }

            jsonBuilder.Append(']');

            return jsonBuilder.ToString();
        }

        public static string toJsonString(Vector3 vector)
        {
            return string.Format("{{\"x\":{0},\"y\":{1},\"z\":{2}}}", vector.X, vector.Y, vector.Z);
        }
    }
}
