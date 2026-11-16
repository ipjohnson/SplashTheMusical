using System.Text;
using SplashTheMusical.Cli;

// The console's own encodings depend on the platform and its code page, and STM reads and writes UTF-8.
var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
using var stdout = new StreamWriter(Console.OpenStandardOutput(), utf8);
using var stderr = new StreamWriter(Console.OpenStandardError(), utf8);
return StmCommand.Run(args, () => new StreamReader(Console.OpenStandardInput(), utf8), stdout, stderr);
