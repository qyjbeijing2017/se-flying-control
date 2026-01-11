using System;
using System.Collections.Generic;
using System.Text;

namespace IngameScript
{
    public class LogSystem
    {
        private Queue<string> logBuffer;
        private int maxLines;

        private bool m_isDirty;
        public bool isDirty
        {
            get { return m_isDirty; }
        }

        public LogSystem(int maxLines = 30)
        {
            this.maxLines = maxLines;
            logBuffer = new Queue<string>(maxLines);
        }

        /// <summary>
        /// 添加日志信息
        /// </summary>
        public void Log(string message)
        {
            logBuffer.Enqueue(message);

            // 保持只有最多maxLines条记录
            while (logBuffer.Count > maxLines)
            {
                logBuffer.Dequeue();
            }
            m_isDirty = true;
        }

        /// <summary>
        /// 清空日志
        /// </summary>
        public void Clear()
        {
            m_isDirty = true;
            logBuffer.Clear();
        }

        public string OutPut()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var line in logBuffer)
            {
                sb.AppendLine(line);
            }
            return sb.ToString();
        }

        public void LogError(string message)
        {
            Log($"[Error] {message}");
        }
    }
}
