// 개발 보조: 컴파일 결과와 규칙 셀프체크 결과를 프로젝트의 Logs 폴더에 텍스트로 남깁니다.
// (Claude 채팅 세션이 에디터 콘솔을 직접 볼 수 없어서, 파일로 결과를 확인하기 위한 통로)
// 게임 코드와 독립된 어셈블리라 게임 코드에 오류가 있어도 이 스크립트는 동작합니다.
using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace PokerTower.EditorTools
{
    [InitializeOnLoad]
    internal static class PokerTowerDevReport
    {
        private static readonly StringBuilder Messages = new StringBuilder();
        private static int _errorCount;

        private static string LogDir
        {
            get { return Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs"); }
        }

        static PokerTowerDevReport()
        {
            CompilationPipeline.compilationStarted += _ => { Messages.Clear(); _errorCount = 0; };
            CompilationPipeline.assemblyCompilationFinished += (assembly, msgs) =>
            {
                foreach (var m in msgs)
                {
                    if (m.type == CompilerMessageType.Error) _errorCount++;
                    Messages.AppendLine(m.type + "\t" + Path.GetFileName(assembly) + "\t" + m.file + ":" + m.line + "\t" + m.message);
                }
            };
            CompilationPipeline.compilationFinished += _ =>
            {
                Write("PokerTower_compile.txt",
                      "time " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\nerrors " + _errorCount + "\n" + Messages);
            };
            // 도메인 리로드 직후(=컴파일 성공 후) 규칙 셀프체크 실행
            EditorApplication.delayCall += RunSelfCheck;
        }

        [MenuItem("PokerTower/Run Self Check")]
        private static void RunSelfCheck()
        {
            string result;
            try
            {
                Type t = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    t = asm.GetType("PokerTower.Core.SelfCheck");
                    if (t != null) break;
                }
                if (t == null) result = "SelfCheck 타입 없음 (PokerTower.Core 미컴파일)";
                else
                {
                    var m = t.GetMethod("Run", BindingFlags.Public | BindingFlags.Static);
                    result = (string)m.Invoke(null, null);
                }
            }
            catch (Exception e)
            {
                result = "SelfCheck 예외: " + e;
            }
            Write("PokerTower_selfcheck.txt", "time " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n" + result);
        }

        private static void Write(string file, string text)
        {
            try
            {
                Directory.CreateDirectory(LogDir);
                File.WriteAllText(Path.Combine(LogDir, file), text, new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PokerTower] 리포트 기록 실패: " + e.Message);
            }
        }
    }
}
