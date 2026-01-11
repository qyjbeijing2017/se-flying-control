using System;

namespace IngameScript
{
    public partial class Program
    {
        /// <summary>
        /// 等待指定秒数
        /// </summary>
        public class WaitForSeconds : YieldInstruction
        {
            private double waitTime;
            private double startTime;
            private Program program;

            public WaitForSeconds(Program program, double seconds)
            {
                this.program = program;
                waitTime = seconds;
                startTime = program.Runtime.TimeSinceLastRun.TotalSeconds;
            }

            public override bool IsDone()
            {
                double currentTime = program.Runtime.TimeSinceLastRun.TotalSeconds;
                return (currentTime - startTime) >= waitTime;
            }
        }
    }
}
