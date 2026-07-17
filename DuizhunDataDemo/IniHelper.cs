using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DuizhunDataDemo
{
    /// <summary>
    /// 简易INI文件读写工具
    /// 格式：[Section] 下 key=value
    /// </summary>
    public static class IniHelper
    {
        private static readonly string _iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");

        public static string IniPath => _iniPath;

        /// <summary>
        /// 读取INI文件指定Section下的key值，不存在则返回defaultValue
        /// </summary>
        public static string ReadValue(string section, string key, string defaultValue = "")
        {
            if (!File.Exists(_iniPath)) return defaultValue;

            string[] lines;
            try { lines = File.ReadAllLines(_iniPath, Encoding.UTF8); }
            catch { return defaultValue; }

            bool inSection = false;
            string sectionHeader = $"[{section}]";

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                if (line.Equals(sectionHeader, StringComparison.OrdinalIgnoreCase))
                {
                    inSection = true;
                    continue;
                }

                // 遇到新的section头，停止
                if (line.StartsWith("[") && inSection)
                    break;

                if (inSection && line.Contains("="))
                {
                    int eqIdx = line.IndexOf('=');
                    string currentKey = line.Substring(0, eqIdx).Trim();
                    string currentVal = line.Substring(eqIdx + 1).Trim();

                    if (currentKey.Equals(key, StringComparison.OrdinalIgnoreCase))
                        return currentVal;
                }
            }

            return defaultValue;
        }

        /// <summary>
        /// 写入INI文件指定Section下的key=value，不存在则自动创建section和key
        /// </summary>
        public static void WriteValue(string section, string key, string value)
        {
            List<string> lines = new List<string>();

            if (File.Exists(_iniPath))
            {
                try { lines.AddRange(File.ReadAllLines(_iniPath, Encoding.UTF8)); }
                catch { }
            }

            string sectionHeader = $"[{section}]";
            int sectionStart = -1;
            int sectionEnd = lines.Count;

            // 查找section位置
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Trim().Equals(sectionHeader, StringComparison.OrdinalIgnoreCase))
                {
                    sectionStart = i;
                    break;
                }
            }

            // section不存在，追加到末尾
            if (sectionStart == -1)
            {
                if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[lines.Count - 1]))
                    lines.Add("");
                lines.Add(sectionHeader);
                lines.Add($"{key}={value}");
                WriteAllLines(lines);
                return;
            }

            // 找到section结尾（下一个section之前）
            for (int i = sectionStart + 1; i < lines.Count; i++)
            {
                if (lines[i].Trim().StartsWith("["))
                {
                    sectionEnd = i;
                    break;
                }
            }

            // 在section内查找key
            for (int i = sectionStart + 1; i < sectionEnd; i++)
            {
                string line = lines[i].Trim();
                if (line.Contains("="))
                {
                    int eqIdx = line.IndexOf('=');
                    string currentKey = line.Substring(0, eqIdx).Trim();
                    if (currentKey.Equals(key, StringComparison.OrdinalIgnoreCase))
                    {
                        lines[i] = $"{key}={value}";
                        WriteAllLines(lines);
                        return;
                    }
                }
            }

            // key不存在，在section末尾插入
            lines.Insert(sectionEnd, $"{key}={value}");
            WriteAllLines(lines);
        }

        /// <summary>
        /// 读取指定section下的所有key=value对
        /// </summary>
        public static Dictionary<string, string> ReadSection(string section)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!File.Exists(_iniPath)) return result;

            string[] lines;
            try { lines = File.ReadAllLines(_iniPath, Encoding.UTF8); }
            catch { return result; }

            bool inSection = false;
            string sectionHeader = $"[{section}]";

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                if (line.Equals(sectionHeader, StringComparison.OrdinalIgnoreCase))
                {
                    inSection = true;
                    continue;
                }

                if (line.StartsWith("[") && inSection)
                    break;

                if (inSection && line.Contains("="))
                {
                    int eqIdx = line.IndexOf('=');
                    string key = line.Substring(0, eqIdx).Trim();
                    string val = line.Substring(eqIdx + 1).Trim();
                    result[key] = val;
                }
            }

            return result;
        }

        /// <summary>
        /// 删除指定section
        /// </summary>
        public static void DeleteSection(string section)
        {
            if (!File.Exists(_iniPath)) return;

            List<string> lines;
            try { lines = new List<string>(File.ReadAllLines(_iniPath, Encoding.UTF8)); }
            catch { return; }

            string sectionHeader = $"[{section}]";
            int start = -1;
            int end = lines.Count;

            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Trim().Equals(sectionHeader, StringComparison.OrdinalIgnoreCase))
                {
                    start = i;
                    continue;
                }
                if (start >= 0 && lines[i].Trim().StartsWith("["))
                {
                    end = i;
                    break;
                }
            }

            if (start >= 0)
            {
                // 删除section前面的空行（如果有）
                int removeStart = start;
                if (removeStart > 0 && string.IsNullOrWhiteSpace(lines[removeStart - 1]))
                    removeStart--;

                lines.RemoveRange(removeStart, end - removeStart);
                WriteAllLines(lines);
            }
        }

        /// <summary>
        /// 读取[Global]下的Schemes列表，逗号分隔
        /// </summary>
        public static List<string> ReadSchemeList()
        {
            string schemes = ReadValue("Global", "Schemes", "");
            var result = new List<string>();
            foreach (string s in schemes.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = s.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    result.Add(trimmed);
            }
            return result;
        }

        /// <summary>
        /// 写入[Global]下的Schemes列表
        /// </summary>
        public static void WriteSchemeList(List<string> schemes)
        {
            WriteValue("Global", "Schemes", string.Join(",", schemes));
        }

        private static void WriteAllLines(List<string> lines)
        {
            try
            {
                File.WriteAllLines(_iniPath, lines, Encoding.UTF8);
            }
            catch { }
        }
    }
}
