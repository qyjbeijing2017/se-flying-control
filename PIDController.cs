using System;

namespace IngameScript
{
    public partial class Program
    {

        public class PIDController
        {
            private double Ts;

            private double K;

            private double b0;

            private double b1;

            private double b2;

            private double a0;

            private double a1;

            private double a2;

            private double y0;

            private double y1;

            private double y2;

            private double e0;

            private double e1;

            private double e2;

            //
            // Summary:
            //     Proportional Gain, consider resetting controller if this parameter is drastically
            //     changed.
            public double Kp { get; set; }

            //
            // Summary:
            //     Integral Gain, consider resetting controller if this parameter is drastically
            //     changed.
            public double Ki { get; set; }

            //
            // Summary:
            //     Derivative Gain, consider resetting controller if this parameter is drastically
            //     changed.
            public double Kd { get; set; }

            //
            // Summary:
            //     Derivative filter coefficient. A smaller N for more filtering. A larger N for
            //     less filtering. Consider resetting controller if this parameter is drastically
            //     changed.
            public double N { get; set; }

            //
            // Summary:
            //     Minimum allowed sample period to avoid dividing by zero! The Ts value can be
            //     mistakenly set to too low of a value or zero on the first iteration. TsMin by
            //     default is set to 1 millisecond.
            public double TsMin { get; set; } = 0.001;

            //
            // Summary:
            //     Upper output limit of the controller. This should obviously be a numerically
            //     greater value than the lower output limit.
            public double OutputUpperLimit { get; set; }

            //
            // Summary:
            //     Lower output limit of the controller This should obviously be a numerically lesser
            //     value than the upper output limit.
            public double OutputLowerLimit { get; set; }

            //
            // Summary:
            //     PID Constructor
            //
            // Parameters:
            //   Kp:
            //     Proportional Gain
            //
            //   Ki:
            //     Integral Gain
            //
            //   Kd:
            //     Derivative Gain
            //
            //   N:
            //     Derivative Filter Coefficient
            //
            //   OutputUpperLimit:
            //     Controller Upper Output Limit
            //
            //   OutputLowerLimit:
            //     Controller Lower Output Limit
            public PIDController(double Kp, double Ki, double Kd, double N, double OutputUpperLimit, double OutputLowerLimit)
            {
                this.Kp = Kp;
                this.Ki = Ki;
                this.Kd = Kd;
                this.N = N;
                this.OutputUpperLimit = OutputUpperLimit;
                this.OutputLowerLimit = OutputLowerLimit;
            }

            //
            // Summary:
            //     Call this function every sample period to get the current controller output.
            //     setpoint and processValue should use the same units.
            //
            // Parameters:
            //   setPoint:
            //     Current Desired Setpoint
            //
            //   processValue:
            //     Current Process Value
            //
            //   ts:
            //     Timespan Since Last Iteration, Use Default Sample Period for First Call
            //
            // Returns:
            //     Current Controller Output
            public double Calculate(double setPoint, double processValue, TimeSpan ts)
            {
                Ts = ((ts.TotalSeconds >= TsMin) ? ts.TotalSeconds : TsMin);
                K = 2.0 / Ts;
                b0 = Math.Pow(K, 2.0) * Kp + K * Ki + Ki * N + K * Kp * N + Math.Pow(K, 2.0) * Kd * N;
                b1 = 2.0 * Ki * N - 2.0 * Math.Pow(K, 2.0) * Kp - 2.0 * Math.Pow(K, 2.0) * Kd * N;
                b2 = Math.Pow(K, 2.0) * Kp - K * Ki + Ki * N - K * Kp * N + Math.Pow(K, 2.0) * Kd * N;
                a0 = Math.Pow(K, 2.0) + N * K;
                a1 = -2.0 * Math.Pow(K, 2.0);
                a2 = Math.Pow(K, 2.0) - K * N;
                e2 = e1;
                e1 = e0;
                e0 = setPoint - processValue;
                y2 = y1;
                y1 = y0;
                y0 = (0.0 - a1) / a0 * y1 - a2 / a0 * y2 + b0 / a0 * e0 + b1 / a0 * e1 + b2 / a0 * e2;
                if (y0 > OutputUpperLimit)
                {
                    y0 = OutputUpperLimit;
                }
                else if (y0 < OutputLowerLimit)
                {
                    y0 = OutputLowerLimit;
                }

                return y0;
            }

            //
            // Summary:
            //     Reset controller history effectively resetting the controller.
            public void ResetController()
            {
                e2 = 0.0;
                e1 = 0.0;
                e0 = 0.0;
                y2 = 0.0;
                y1 = 0.0;
                y0 = 0.0;
            }
        }
    }
}
