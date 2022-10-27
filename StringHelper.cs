using Min.Log;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Collections;
using System.Reflection;
using System.Linq;

using Min.Log;

namespace Min.Log
{
    internal static class StringHelper
    {
        static StringHelper()
        {
            var dir = "";
            // 命令参数
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i].EqualIgnoreCase("-BasePath", "--BasePath") && i + 1 < args.Length)
                {
                    dir = args[i + 1];
                    break;
                }
            }

            // 环境变量
            if (dir.IsNullOrEmpty()) dir = Environment.GetEnvironmentVariable("BasePath");

            // 最终取应用程序域。Linux下编译为单文件时，应用程序释放到临时目录，应用程序域基路径不对，当前目录也不一定正确，唯有进程路径正确
            if (dir.IsNullOrEmpty()) dir = AppDomain.CurrentDomain.BaseDirectory;

            BasePath = GetPath(dir, 1);
        }

        /// <summary>基础目录。GetBasePath依赖于此，默认为当前应用程序域基础目录。用于X组件内部各目录，专门为函数计算而定制</summary>
        /// <remarks>
        /// 为了适应函数计算，该路径将支持从命令行参数和环境变量读取
        /// </remarks>
        internal static String BasePath { get; set; }

        /// <summary>忽略大小写的字符串相等比较，判断是否与任意一个待比较字符串相等</summary>
        /// <param name="value">字符串</param>
        /// <param name="strs">待比较字符串数组</param>
        /// <returns></returns>
        internal static Boolean EqualIgnoreCase(this String? value, params String[] strs)
        {
            foreach (var item in strs)
            {
                if (String.Equals(value, item, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>指示指定的字符串是 null 还是 String.Empty 字符串</summary>
        /// <param name="value">字符串</param>
        /// <returns></returns>
        internal static Boolean IsNullOrEmpty(this String? value) => value == null || value.Length <= 0;

        /// <summary>确保目录存在，若不存在则创建</summary>
        /// <remarks>
        /// 斜杠结尾的路径一定是目录，无视第二参数；
        /// 默认是文件，这样子只需要确保上一层目录存在即可，否则如果把文件当成了目录，目录的创建会导致文件无法创建。
        /// </remarks>
        /// <param name="path">文件路径或目录路径，斜杠结尾的路径一定是目录，无视第二参数</param>
        /// <param name="isfile">该路径是否是否文件路径。文件路径需要取目录部分</param>
        /// <returns></returns>
        internal static String EnsureDirectory(this String path, Boolean isfile = true)
        {
            if (String.IsNullOrEmpty(path)) return path;

            path = path.GetFullPath();
            if (File.Exists(path) || Directory.Exists(path)) return path;

            var dir = path;
            // 斜杠结尾的路径一定是目录，无视第二参数
            if (dir[dir.Length - 1] == Path.DirectorySeparatorChar)
                dir = Path.GetDirectoryName(path);
            else if (isfile)
                dir = Path.GetDirectoryName(path);

            /*!!! 基础类库的用法应该有明确的用途，而不是通过某些小伎俩去让人猜测 !!!*/

            //// 如果有圆点说明可能是文件
            //var p1 = dir.LastIndexOf('.');
            //if (p1 >= 0)
            //{
            //    // 要么没有斜杠，要么圆点必须在最后一个斜杠后面
            //    var p2 = dir.LastIndexOf('\\');
            //    if (p2 < 0 || p2 < p1) dir = Path.GetDirectoryName(path);
            //}

            if (!String.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            return path;
        }

        /// <summary>获取文件或目录基于应用程序域基目录的全路径，过滤相对目录</summary>
        /// <remarks>不确保目录后面一定有分隔符，是否有分隔符由原始路径末尾决定</remarks>
        /// <param name="path">文件或目录</param>
        /// <returns></returns>
        internal static String GetFullPath(this String path)
        {
            if (String.IsNullOrEmpty(path)) return path;

            return GetPath(path, 1);
        }

        private static String GetPath(String path, Int32 mode)
        {
            // 处理路径分隔符，兼容Windows和Linux
            var sep = Path.DirectorySeparatorChar;
            var sep2 = sep == '/' ? '\\' : '/';
            path = path.Replace(sep2, sep);

            var dir = "";
            switch (mode)
            {
                case 1:
                    dir = AppDomain.CurrentDomain.BaseDirectory;
                    break;

                case 2:
                    dir = BasePath;
                    break;

                case 3:
                    dir = Environment.CurrentDirectory;
                    break;

                default:
                    break;
            }
            if (dir.IsNullOrEmpty()) return Path.GetFullPath(path);

            // 处理网络路径
            if (path.StartsWith(@"\\", StringComparison.Ordinal)) return Path.GetFullPath(path);

            // 考虑兼容Linux
            if (!Runtime.Mono)
            {
                //if (!Path.IsPathRooted(path))
                //!!! 注意：不能直接依赖于Path.IsPathRooted判断，/和\开头的路径虽然是绝对路径，但是它们不是驱动器级别的绝对路径
                if (/*path[0] == sep ||*/ path[0] == sep2 || !Path.IsPathRooted(path))
                {
                    path = path.TrimStart('~');

                    path = path.TrimStart(sep);
                    path = Path.Combine(dir, path);
                }
            }
            else
            {
                if (!path.StartsWith(dir))
                {
                    // path目录存在，不用再次拼接
                    if (!Directory.Exists(path))
                    {
                        path = path.TrimStart(sep);
                        path = Path.Combine(dir, path);
                    }
                }
            }

            return Path.GetFullPath(path);
        }

        /// <summary>合并多段路径</summary>
        /// <param name="path"></param>
        /// <param name="ps"></param>
        /// <returns></returns>
        internal static String CombinePath(this String path, params String[] ps)
        {
            if (ps == null || ps.Length < 1) return path;
            if (path == null) path = String.Empty;

            //return Path.Combine(path, path2);
            foreach (var item in ps)
            {
                if (!item.IsNullOrEmpty()) path = Path.Combine(path, item);
            }
            return path;
        }

        /// <summary>获取文件或目录的全路径，过滤相对目录。用于X组件内部各目录，专门为函数计算而定制</summary>
        /// <remarks>不确保目录后面一定有分隔符，是否有分隔符由原始路径末尾决定</remarks>
        /// <param name="path">文件或目录</param>
        /// <returns></returns>
        internal static String GetBasePath(this String path)
        {
            if (String.IsNullOrEmpty(path)) return path;

            return GetPath(path, 2);
        }

        /// <summary>文件路径作为文件信息</summary>
        /// <param name="file"></param>
        /// <returns></returns>
        internal static FileInfo AsFile(this String file) => new(file.GetFullPath());

        /// <summary>路径作为目录信息</summary>
        /// <param name="dir"></param>
        /// <returns></returns>
        internal static DirectoryInfo AsDirectory(this String dir) => new(dir.GetFullPath());

        /// <summary>尝试销毁对象，如果有<see cref="IDisposable"/>则调用</summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        internal static Object? TryDispose(this Object? obj)
        {
            if (obj == null) return obj;

            // 列表元素销毁
            if (obj is IEnumerable ems)
            {
                // 对于枚举成员，先考虑添加到列表，再逐个销毁，避免销毁过程中集合改变
                if (obj is not IList list)
                {
                    list = new List<Object>();
                    foreach (var item in ems)
                    {
                        if (item is IDisposable) list.Add(item);
                    }
                }
                foreach (var item in list)
                {
                    if (item is IDisposable disp)
                    {
                        try
                        {
                            //(item as IDisposable).TryDispose();
                            // 只需要释放一层，不需要递归
                            // 因为一般每一个对象负责自己内部成员的释放
                            disp.Dispose();
                        }
                        catch { }
                    }
                }
            }
            // 对象销毁
            if (obj is IDisposable disp2)
            {
                try
                {
                    disp2.Dispose();
                }
                catch { }
            }

            return obj;
        }

        /// <summary>把一个方法转为泛型委托，便于快速反射调用</summary>
        /// <typeparam name="TFunc"></typeparam>
        /// <param name="method"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        internal static TFunc As<TFunc>(this MethodInfo method, Object target = null)
        {
            if (method == null) return default;

            if (target == null)
                return (TFunc)(Object)Delegate.CreateDelegate(typeof(TFunc), method, true);
            else
                return (TFunc)(Object)Delegate.CreateDelegate(typeof(TFunc), target, method, true);
        }

        /// <summary>把一个列表组合成为一个字符串，默认逗号分隔</summary>
        /// <param name="value"></param>
        /// <param name="separator">组合分隔符，默认逗号</param>
        /// <param name="func">把对象转为字符串的委托</param>
        /// <returns></returns>
        internal static String Join<T>(this IEnumerable<T> value, String separator = ",", Func<T, Object?>? func = null)
        {
            var sb = new StringBuilder();
            if (value != null)
            {
                if (func == null) func = obj => obj;
                foreach (var item in value)
                {
                    sb.Separate(separator).Append(func(item));
                }
            }
            var result = sb.ToString();
            sb.Clear();
            return result;
        }

        /// <summary>追加分隔符字符串，忽略开头，常用于拼接</summary>
        /// <param name="sb">字符串构造者</param>
        /// <param name="separator">分隔符</param>
        /// <returns></returns>
        internal static StringBuilder Separate(this StringBuilder sb, String separator)
        {
            if (/*sb == null ||*/ String.IsNullOrEmpty(separator)) return sb;

            if (sb.Length > 0) sb.Append(separator);

            return sb;
        }

        internal static string GetMessage(this Exception ex)
        {
            var msg = ex + "";
            if (msg.IsNullOrEmpty()) return null;

            var ss = msg.Split(Environment.NewLine);
            var ns = ss.Where(e =>
            !e.StartsWith("---") &&
            !e.Contains("System.Runtime.ExceptionServices") &&
            !e.Contains("System.Runtime.CompilerServices"));

            msg = ns.Join(Environment.NewLine);

            return msg;
        }

        /// <summary>忽略大小写的字符串结束比较，判断是否以任意一个待比较字符串结束</summary>
        /// <param name="value">字符串</param>
        /// <param name="strs">待比较字符串数组</param>
        /// <returns></returns>
        internal static Boolean EndsWithIgnoreCase(this String? value, params String[] strs)
        {
            if (value == null || String.IsNullOrEmpty(value)) return false;

            foreach (var item in strs)
            {
                if (value.EndsWith(item, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }  /// <summary>忽略大小写的字符串开始比较，判断是否与任意一个待比较字符串开始</summary>

           /// <param name="value">字符串</param>
           /// <param name="strs">待比较字符串数组</param>
           /// <returns></returns>
        internal static Boolean StartsWithIgnoreCase(this String? value, params String[] strs)
        {
            if (value == null || String.IsNullOrEmpty(value)) return false;

            foreach (var item in strs)
            {
                if (value.StartsWith(item, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>从当前字符串开头移除另一字符串，不区分大小写，循环多次匹配前缀</summary>
        /// <param name="str">当前字符串</param>
        /// <param name="starts">另一字符串</param>
        /// <returns></returns>
        internal static String TrimStart(this String str, params String[] starts)
        {
            if (String.IsNullOrEmpty(str)) return str;
            if (starts == null || starts.Length < 1 || String.IsNullOrEmpty(starts[0])) return str;

            for (var i = 0; i < starts.Length; i++)
            {
                if (str.StartsWith(starts[i], StringComparison.OrdinalIgnoreCase))
                {
                    str = str.Substring(starts[i].Length);
                    if (String.IsNullOrEmpty(str)) break;

                    // 从头开始
                    i = -1;
                }
            }
            return str;
        }

        /// <summary>从当前字符串结尾移除另一字符串，不区分大小写，循环多次匹配后缀</summary>
        /// <param name="str">当前字符串</param>
        /// <param name="ends">另一字符串</param>
        /// <returns></returns>
        internal static String TrimEnd(this String str, params String[] ends)
        {
            if (String.IsNullOrEmpty(str)) return str;
            if (ends == null || ends.Length < 1 || String.IsNullOrEmpty(ends[0])) return str;

            for (var i = 0; i < ends.Length; i++)
            {
                if (str.EndsWith(ends[i], StringComparison.OrdinalIgnoreCase))
                {
                    str = str.Substring(0, str.Length - ends[i].Length);
                    if (String.IsNullOrEmpty(str)) break;

                    // 从头开始
                    i = -1;
                }
            }
            return str;
        }

        /// <summary>确保字符串以指定的另一字符串开始，不区分大小写</summary>
        /// <param name="str">字符串</param>
        /// <param name="start"></param>
        /// <returns></returns>
        internal static String EnsureStart(this String str, String start)
        {
            if (String.IsNullOrEmpty(start)) return str;
            if (String.IsNullOrEmpty(str)) return start;

            if (str.StartsWith(start, StringComparison.OrdinalIgnoreCase)) return str;

            return start + str;
        }

        /// <summary>确保字符串以指定的另一字符串结束，不区分大小写</summary>
        /// <param name="str">字符串</param>
        /// <param name="end"></param>
        /// <returns></returns>
        internal static String EnsureEnd(this String str, String end)
        {
            if (String.IsNullOrEmpty(end)) return str;
            if (String.IsNullOrEmpty(str)) return end;

            if (str.EndsWith(end, StringComparison.OrdinalIgnoreCase)) return str;

            return str + end;
        }  /// <summary>获取自定义属性的值。可用于ReflectionOnly加载的程序集</summary>

           /// <typeparam name="TAttribute"></typeparam>
           /// <typeparam name="TResult"></typeparam>
           /// <returns></returns>
        internal static TResult GetCustomAttributeValue<TAttribute, TResult>(this Assembly target) where TAttribute : Attribute
        {
            if (target == null) return default;

            // CustomAttributeData可能会导致只反射加载，需要屏蔽内部异常
            try
            {
                var list = CustomAttributeData.GetCustomAttributes(target);
                if (list == null || list.Count < 1) return default;

                foreach (var item in list)
                {
                    if (typeof(TAttribute) != item.Constructor.DeclaringType) continue;

                    var args = item.ConstructorArguments;
                    if (args != null && args.Count > 0) return (TResult)args[0].Value;
                }
            }
            catch { }

            return default;
        }
    }
}