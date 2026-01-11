using System;
using System.Collections;
using System.Collections.Generic;

namespace IngameScript
{
    public partial class Program
    {

        /// <summary>
        /// 协程管理器类 - 支持嵌套协程
        /// </summary>
        public class CoroutineManager
        {
            private List<Coroutine> coroutines = new List<Coroutine>();
            private LogSystem logSystem;

            public CoroutineManager(LogSystem logSystem)
            {
                this.logSystem = logSystem;
            }

            /// <summary>
            /// 启动一个协程
            /// </summary>
            public Coroutine Start(IEnumerator routine)
            {
                var coroutine = new Coroutine(routine, logSystem);
                coroutines.Add(coroutine);
                return coroutine;
            }

            /// <summary>
            /// 停止一个协程
            /// </summary>
            public void Stop(Coroutine coroutine)
            {
                if (coroutines.Contains(coroutine))
                {
                    coroutine.Stop();
                    coroutines.Remove(coroutine);
                }
            }

            /// <summary>
            /// 停止所有协程
            /// </summary>
            public void StopAll()
            {
                foreach (var coroutine in coroutines)
                {
                    coroutine.Stop();
                }
                coroutines.Clear();
            }

            /// <summary>
            /// 更新所有协程
            /// </summary>
            public void Update()
            {
                // 移除已完成的协程
                coroutines.RemoveAll(c => c.IsDone());
            }

            /// <summary>
            /// 获取当前运行的协程数量
            /// </summary>
            public int Count => coroutines.Count;
        }


        /// <summary>
        /// Yield指令基类
        /// </summary>
        public abstract class YieldInstruction
        {
            public abstract bool IsDone();
        }

        /// <summary>
        /// 协程类 - 继承YieldInstruction，支持嵌套
        /// </summary>
        public class Coroutine : YieldInstruction
        {
            private IEnumerator routine;
            private YieldInstruction currentWait = null;
            private bool isRunning = true;
            LogSystem logSystem;

            public Coroutine(IEnumerator routine, LogSystem logSystem)
            {
                this.routine = routine;
                this.logSystem = logSystem;
            }

            /// <summary>
            /// 更新协程，返回是否完成
            /// </summary>
            public override bool IsDone()
            {
                if (!isRunning || routine == null) return true;

                try
                {
                    // 如果有等待中的YieldInstruction，先检查是否完成
                    if (currentWait != null)
                    {
                        if (!currentWait.IsDone())
                        {
                            return false; // 还在等待
                        }
                        currentWait = null; // 等待完成，清空并继续执行
                    }

                    // 执行协程
                    if (routine.MoveNext())
                    {
                        var value = routine.Current;

                        if (value is YieldInstruction)
                        {
                            // 如果返回值是YieldInstruction，记录等待
                            currentWait = value as YieldInstruction;
                            return false;
                        }

                        // null或其他值，继续执行下一帧
                        return false;
                    }
                    else
                    {
                        // 协程已完成
                        isRunning = false;
                        return true;
                    }
                }
                catch
                {
                    // 发生异常时停止协程
                    isRunning = false;
                    currentWait = null;
                    logSystem.Log("Error: 协程执行时发生异常，协程已停止。");
                    return true;
                }
            }

            /// <summary>
            /// 停止协程
            /// </summary>
            public void Stop()
            {
                isRunning = false;
                currentWait = null;
            }

            /// <summary>
            /// 协程是否正在运行
            /// </summary>
            public bool IsRunning => isRunning;
        }

    }
}