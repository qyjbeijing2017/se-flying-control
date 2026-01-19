using ParallelTasks;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using SpaceEngineers.Game.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Http.Headers;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    public partial class Program : MyGridProgram
    {
        LogSystem logSystem;
        List<IMyShipController> controllers = new List<IMyShipController>();
        List<IMyThrust> thrusters = new List<IMyThrust>();
        List<IMyGyro> gyros = new List<IMyGyro>();

        float speedTarget = 5.0f; // 目标速度 m/s

        PIDController rotationPID;
        PIDController speedPID;

        bool isRunning
        {
            get
            {
                return controllers.Count > 0 || thrusters.Count > 0 || gyros.Count > 0;
            }
        }
        IMyShipController controller
        {
            get
            {
                if (controllers.Count > 0)
                {
                    return controllers.Find(c => c.IsUnderControl) ?? controllers[0];
                }
                else
                    return null;
            }
        }

        public Program()
        {
            logSystem = new LogSystem(100);
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            var parameters = new ParameterParser(Me.CustomData);
            speedTarget = parameters.GetFloat("speedTarget", 20.0f);
            var rotationKp = parameters.GetFloat("rotationKp", 3.5f);
            var rotationKi = parameters.GetFloat("rotationKi", 0.00f);
            var rotationKd = parameters.GetFloat("rotationKd", 0.00f);
            var rotationN = parameters.GetFloat("rotationN", 10.0f);
            rotationPID = new PIDController(rotationKp, rotationKi, rotationKd, rotationN, 1.0, -1.0);
            var speedKp = parameters.GetFloat("speedKp", 6.0f);
            var speedKi = parameters.GetFloat("speedKi", 0.00f);
            var speedKd = parameters.GetFloat("speedKd", 0.00f);
            var speedN = parameters.GetFloat("speedN", 10.0f);
            speedPID = new PIDController(speedKp, speedKi, speedKd, speedN, 1.0, -1.0);

            GridTerminalSystem.GetBlocksOfType(controllers, c => c.CubeGrid == Me.CubeGrid);
            if (controllers.Count == 0)
            {
                logSystem.LogError("未找到任何飞行控制器");
                return;
            }
            GridTerminalSystem.GetBlocksOfType(thrusters, t => t.WorldMatrix.Forward == controller.WorldMatrix.Down && t.CubeGrid == Me.CubeGrid);
            if (thrusters.Count == 0)
            {
                logSystem.LogError("未找到任何推进器");
                return;
            }
            GridTerminalSystem.GetBlocksOfType(gyros, g => g.CubeGrid == Me.CubeGrid);
            if (gyros.Count == 0)
            {
                logSystem.LogError("未找到任何陀螺仪");
                return;
            }

            logSystem.Log($"已找到{controllers.Count}个飞行控制器，{thrusters.Count}个推进器，{gyros.Count}个陀螺仪");
            logSystem.Log("飞行控制器V0.6已启动");
        }

        public void Save()
        {
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (isRunning)
                Update();
            if (logSystem.isDirty)
                Echo(logSystem.OutPut());
        }

        Vector3D targetVelocity
        {
            get
            {
                var indicator = controller.MoveIndicator;
                var gravity = controller.GetNaturalGravity();
                Vector3D right = controller.WorldMatrix.Right;
                Vector3D up = controller.WorldMatrix.Up;
                Vector3D backward = controller.WorldMatrix.Backward;
                if (gravity.LengthSquared() > 0.01)
                {
                    up -= gravity;
                    up.Normalize();
                    right = Vector3D.Cross(gravity, controller.WorldMatrix.Forward);
                    right.Normalize();
                    backward = Vector3D.Cross(right, up);
                    backward.Normalize();
                }
                var dir = indicator.X * right + indicator.Y * up + indicator.Z * backward;
                if (dir.LengthSquared() < 0.01)
                    return Vector3D.Zero;
                return dir.Normalized() * speedTarget;
            }

        }

        private void GrayRotate(Vector3D omegaTarget)
        {
            foreach (var gyro in gyros)
            {
                gyro.GyroOverride = true;
                MatrixD invGyro = MatrixD.Transpose(gyro.WorldMatrix);
                Vector3D localOmega = Vector3D.TransformNormal(omegaTarget, invGyro);
                gyro.Pitch = (float)localOmega.X;
                gyro.Yaw = (float)localOmega.Y;
                gyro.Roll = (float)localOmega.Z;
            }
        }

        private void RotateTo(Vector3D from, Vector3D target)
        {
            Vector3D axis = Vector3D.Cross(from, target);
            double cosAngle = Vector3D.Dot(from, target);
            double sinAngle = axis.Length();
            double angle = MathHelper.Min(Math.Atan2(sinAngle, cosAngle), MathHelper.ToRadians(30));
            axis.Normalize();
            Vector3D omegaTarget = axis.Normalized() * rotationPID.Calculate(0.0, angle, Runtime.TimeSinceLastRun);
            GrayRotate(omegaTarget);
        }

        Vector3D targetAcceleration
        {
            get
            {
                Vector3D gravity = controller.GetNaturalGravity();
                Vector3D currentVelocity = controller.GetShipVelocities().LinearVelocity;
                Vector3D velocityError = targetVelocity - currentVelocity;
                if (velocityError.LengthSquared() > 3)
                {
                    velocityError = velocityError.Normalized() * 3;
                }
                Vector3D desiredAcceleration = velocityError * speedPID.Calculate(0.0, velocityError.Length(), Runtime.TimeSinceLastRun);
                return desiredAcceleration - gravity;
            }
        }

        private void Update()
        {
            Vector3D gravity = controller.GetNaturalGravity();
            Vector3D desiredAcceleration = targetAcceleration;
            ThrustOverride(desiredAcceleration);
            RotateTo(controller.WorldMatrix.Up, desiredAcceleration);
            rotationPID.ResetController();
            // speedPID.ResetController();
        }

        private void ThrustOverride(Vector3D desiredAcc)
        {
            Vector3D from = Vector3D.Normalize(controller.WorldMatrix.Up);
            Vector3D target = Vector3D.Normalize(desiredAcc);
            Vector3D axis = Vector3D.Cross(from, target);
            double cosAngle = Vector3D.Dot(from, target);
            double sinAngle = axis.Length();
            double angle = Math.Atan2(sinAngle, cosAngle);
            if (angle > MathHelper.ToRadians(5))
            {
                // 角度过大，停止推进器
                foreach (var thruster in thrusters)
                {
                    thruster.ThrustOverridePercentage = 0f;
                }
                return;
            }

            float totalThrust = 0f;
            foreach (var thruster in thrusters)
            {
                totalThrust += thruster.MaxEffectiveThrust;
            }
            float requiredThrust = (float)(desiredAcc.Length() * controller.CalculateShipMass().TotalMass);
            float thrustPercent = MathHelper.Clamp(requiredThrust / totalThrust, 0f, 1f);
            foreach (var thruster in thrusters)
            {
                thruster.ThrustOverridePercentage = thrustPercent;
            }
        }

    }
}
