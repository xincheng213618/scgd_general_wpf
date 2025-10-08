using System;
using System.Text;
using System.Windows.Media;

namespace ColorVision.Solution
{
    /// <summary>
    /// 测试 ANSI 转义序列解析器的示例代码
    /// Test examples for ANSI escape sequence parser
    /// </summary>
    public static class AnsiParserTests
    {
        /// <summary>
        /// 生成各种 ANSI 颜色和格式的测试字符串
        /// </summary>
        public static string[] GetTestStrings()
        {
            return new[]
            {
                // 基础颜色测试 - Basic color tests
                "\x1b[31mRed text\x1b[0m",
                "\x1b[32mGreen text\x1b[0m",
                "\x1b[33mYellow text\x1b[0m",
                "\x1b[34mBlue text\x1b[0m",
                "\x1b[35mMagenta text\x1b[0m",
                "\x1b[36mCyan text\x1b[0m",
                "\x1b[37mWhite text\x1b[0m",
                
                // 高亮颜色测试 - Bright color tests
                "\x1b[91mBright Red\x1b[0m",
                "\x1b[92mBright Green\x1b[0m",
                "\x1b[93mBright Yellow\x1b[0m",
                "\x1b[94mBright Blue\x1b[0m",
                
                // 背景颜色测试 - Background color tests
                "\x1b[41mRed background\x1b[0m",
                "\x1b[42mGreen background\x1b[0m",
                "\x1b[44mBlue background\x1b[0m",
                
                // 文本格式测试 - Text formatting tests
                "\x1b[1mBold text\x1b[0m",
                "\x1b[3mItalic text\x1b[0m",
                "\x1b[4mUnderlined text\x1b[0m",
                "\x1b[1;4mBold and underlined\x1b[0m",
                
                // 组合测试 - Combined tests
                "\x1b[1;31mBold Red\x1b[0m",
                "\x1b[4;32mUnderlined Green\x1b[0m",
                "\x1b[1;3;4;33mBold Italic Underlined Yellow\x1b[0m",
                
                // 前景+背景测试 - Foreground + Background tests
                "\x1b[31;42mRed on Green\x1b[0m",
                "\x1b[33;44mYellow on Blue\x1b[0m",
                "\x1b[37;41mWhite on Red\x1b[0m",
                
                // 256色测试 - 256-color tests
                "\x1b[38;5;196mRed (256-color)\x1b[0m",
                "\x1b[38;5;46mGreen (256-color)\x1b[0m",
                "\x1b[38;5;21mBlue (256-color)\x1b[0m",
                "\x1b[48;5;226mYellow background (256-color)\x1b[0m",
                
                // RGB真彩色测试 - RGB true color tests
                "\x1b[38;2;255;100;50mOrange (RGB)\x1b[0m",
                "\x1b[38;2;100;200;255mLight Blue (RGB)\x1b[0m",
                "\x1b[38;2;255;20;147mDeep Pink (RGB)\x1b[0m",
                
                // 复杂混合测试 - Complex mixed tests
                "Normal \x1b[1mBold\x1b[0m Normal \x1b[31mRed\x1b[0m Normal",
                "\x1b[32mGreen \x1b[1mBold Green\x1b[22m Normal Green\x1b[0m",
                "Text with \x1b[4munderline\x1b[24m and \x1b[3mitalic\x1b[23m parts",
                
                // 渐变灰度测试 - Grayscale gradient test
                "\x1b[38;5;232m▓\x1b[38;5;236m▓\x1b[38;5;240m▓\x1b[38;5;244m▓\x1b[38;5;248m▓\x1b[38;5;252m▓\x1b[0m Grayscale",
                
                // 彩虹测试 - Rainbow test
                "\x1b[31m█\x1b[33m█\x1b[32m█\x1b[36m█\x1b[34m█\x1b[35m█\x1b[0m Rainbow",
                
                // PowerShell 风格测试 - PowerShell-style tests
                "\x1b[32mSUCCESS:\x1b[0m Operation completed",
                "\x1b[31mERROR:\x1b[0m Something went wrong",
                "\x1b[33mWARNING:\x1b[0m Please check this",
                "\x1b[36mINFO:\x1b[0m For your information",
                
                // Git 风格测试 - Git-style tests
                "\x1b[32m+ Added line\x1b[0m",
                "\x1b[31m- Removed line\x1b[0m",
                "\x1b[36m@@ Changed section @@\x1b[0m",
                
                // 表格测试 - Table test
                "\x1b[1;4mHeader\x1b[0m | \x1b[1;4mValue\x1b[0m",
                "\x1b[32mPass\x1b[0m   | \x1b[1m100%\x1b[0m",
                "\x1b[31mFail\x1b[0m   | \x1b[1m0%\x1b[0m",
            };
        }

        /// <summary>
        /// 打印所有测试字符串 (用于调试)
        /// </summary>
        public static void PrintTests()
        {
            Console.WriteLine("=== ANSI Escape Sequence Parser Tests ===\n");
            
            var tests = GetTestStrings();
            for (int i = 0; i < tests.Length; i++)
            {
                Console.WriteLine($"Test {i + 1}: {tests[i]}");
            }
            
            Console.WriteLine("\n=== End of Tests ===");
        }

        /// <summary>
        /// 验证解析器基本功能
        /// </summary>
        public static bool VerifyParser()
        {
            try
            {
                // 测试基本颜色解析
                var result1 = AnsiEscapeSequenceParser.Parse(
                    "\x1b[31mRed\x1b[0m", 
                    Colors.White, 
                    Colors.Black
                );
                
                if (result1.Count == 0)
                {
                    Console.WriteLine("❌ Parser returned no results");
                    return false;
                }

                // 测试颜色代码解析
                var result2 = AnsiEscapeSequenceParser.Parse(
                    "\x1b[1;31;44mBold Red on Blue\x1b[0m",
                    Colors.White,
                    Colors.Black
                );
                
                if (result2.Count == 0)
                {
                    Console.WriteLine("❌ Parser failed on complex codes");
                    return false;
                }

                // 测试256色解析
                var result3 = AnsiEscapeSequenceParser.Parse(
                    "\x1b[38;5;196mRed 256\x1b[0m",
                    Colors.White,
                    Colors.Black
                );
                
                if (result3.Count == 0)
                {
                    Console.WriteLine("❌ Parser failed on 256-color");
                    return false;
                }

                // 测试RGB解析
                var result4 = AnsiEscapeSequenceParser.Parse(
                    "\x1b[38;2;255;0;0mRed RGB\x1b[0m",
                    Colors.White,
                    Colors.Black
                );
                
                if (result4.Count == 0)
                {
                    Console.WriteLine("❌ Parser failed on RGB color");
                    return false;
                }

                Console.WriteLine("✅ All parser verification tests passed!");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Parser verification failed with exception: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 生成彩色演示文本
        /// </summary>
        public static string GenerateColorDemo()
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("\x1b[1;36m╔════════════════════════════════════════╗\x1b[0m");
            sb.AppendLine("\x1b[1;36m║   ANSI Color Terminal Demo            ║\x1b[0m");
            sb.AppendLine("\x1b[1;36m╚════════════════════════════════════════╝\x1b[0m");
            sb.AppendLine();
            
            sb.AppendLine("\x1b[1mBasic Colors:\x1b[0m");
            sb.AppendLine("\x1b[30m■\x1b[31m■\x1b[32m■\x1b[33m■\x1b[34m■\x1b[35m■\x1b[36m■\x1b[37m■\x1b[0m");
            sb.AppendLine();
            
            sb.AppendLine("\x1b[1mBright Colors:\x1b[0m");
            sb.AppendLine("\x1b[90m■\x1b[91m■\x1b[92m■\x1b[93m■\x1b[94m■\x1b[95m■\x1b[96m■\x1b[97m■\x1b[0m");
            sb.AppendLine();
            
            sb.AppendLine("\x1b[1mText Formatting:\x1b[0m");
            sb.AppendLine("\x1b[1mBold\x1b[0m | \x1b[3mItalic\x1b[0m | \x1b[4mUnderline\x1b[0m | \x1b[1;3;4mAll\x1b[0m");
            sb.AppendLine();
            
            sb.AppendLine("\x1b[1mStatus Messages:\x1b[0m");
            sb.AppendLine("\x1b[32m✓ Success\x1b[0m");
            sb.AppendLine("\x1b[33m⚠ Warning\x1b[0m");
            sb.AppendLine("\x1b[31m✗ Error\x1b[0m");
            sb.AppendLine("\x1b[36mℹ Info\x1b[0m");
            
            return sb.ToString();
        }
    }
}
