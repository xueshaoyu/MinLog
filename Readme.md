## 一个小小的日志组件，方便但项目输出调试日志文件，临时性代码之必备。支持net7.0,net6.0,net5.0,net3.1
## 日志组件  脱胎于大石头的Newlife.Core，适用于小项目日志输出，调试日志
统一ILog接口，自动实现日志截断，删除
用于控制台程序打印，netcore2.1/3.1 net5.0/6.0日志输出。
引用了配置文件相关第三方组件。
Microsoft.Extensions.Configuration
Microsoft.Extensions.Configuration.Binder
Microsoft.Extensions.Configuration.EnvironmentVariables
Microsoft.Extensions.Configuration.Json


## 使用方法
1.初始化调用方法
XTrace.UseConsole();
2.日志等级
Info等级：XTrace.WriteLine("");//便于全局替换Console.WriteLine()…
Debug等级：XTrace.WriteDebug("");
Error等级：XTrace.WriteError(Exception ex);
Fatal等级：XTrace.WriteFatal("");
Warn等级：XTrace.WriteWarn("");
3.特殊日志文件写入
将文本写入指定文件XTrace.WriteFile(fileName,content);
4.配置方法参考LogSettings.json