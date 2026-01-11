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
        List<IMyThrust> thrusters = new List<IMyThrust>();
        List<IMyGyro> gyros = new List<IMyGyro>();
        IMyShipController controller;

        bool isRunning = false;

        float speedTarget = 5.0f; // 目标速度 m/s
        float rotationTarget = 0.3f; // 目标旋转速度 弧度/s

        PIDController rotationPID;
        PIDController speedPID;

        public Program()
        {
            logSystem = new LogSystem(100);
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            var parameters = new ParameterParser(Me.CustomData);
            var controlName = parameters.Get("controlName", "Controller");

            controller = GridTerminalSystem.GetBlockWithName(controlName) as IMyShipController;
            if (controller == null)
            {
                logSystem.LogError($"未找到名为 '{controlName}' 的控制器");
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
            speedTarget = parameters.GetFloat("speedTarget", 5.0f);
            rotationTarget = parameters.GetFloat("rotationTarget", 0.3f);
            var rotationKp = parameters.GetFloat("rotationKp", 3.5f);
            var rotationKi = parameters.GetFloat("rotationKi", 0.01f);
            var rotationKd = parameters.GetFloat("rotationKd", 0.01f);
            var rotationN = parameters.GetFloat("rotationN", 10.0f);
            rotationPID = new PIDController(rotationKp, rotationKi, rotationKd, rotationN, 1.0, -1.0);
            var speedKp = parameters.GetFloat("speedKp", 3.5f);
            var speedKi = parameters.GetFloat("speedKi", 0.01f);
            var speedKd = parameters.GetFloat("speedKd", 0.01f);
            var speedN = parameters.GetFloat("speedN", 10.0f);
            speedPID = new PIDController(speedKp, speedKi, speedKd, speedN, 1.0, -1.0);

            isRunning = true;
            logSystem.Log("飞行控制器已启动");
        }

        public void Save()
        {
        }

        public void Main(string argument, UpdateType updateSource)
        {

            if (isRunning)
            {
                // 主逻辑代码
                Update();
            }

            if (logSystem.isDirty)
                Echo(logSystem.OutPut());
        }

        private Vector3D TargetVectory(Vector3D gravity)
        {
            Vector3D moveVector = controller.MoveIndicator;
            Vector3D targetVectory = Vector3D.Zero;
            if (moveVector.Length() > 0.01)
            {
                Vector3D forward = Vector3D.Normalize(controller.WorldMatrix.Forward);
                Vector3D right = Vector3D.Normalize(controller.WorldMatrix.Right);
                Vector3D up = Vector3D.Normalize(controller.WorldMatrix.Up);
                if (gravity.Length() > 0.01)
                {
                    up = Vector3D.Normalize(-gravity);
                    forward = Vector3D.Normalize(Vector3D.Cross(controller.WorldMatrix.Left, up));
                    right = Vector3D.Normalize(Vector3D.Cross(forward, up));
                }
                targetVectory += moveVector.Z * forward;
                targetVectory += moveVector.X * right;
                targetVectory += moveVector.Y * up;
                targetVectory = Vector3D.Normalize(targetVectory);
            }
            return targetVectory;
        }


        private Vector3D calcAcc(Vector3D targetVectory, Vector3D gravity)
        {
            var error = targetVectory * speedTarget - controller.GetShipVelocities().LinearVelocity;
            float pidOut = (float)speedPID.Calculate(0, error.Length(), TimeSpan.FromSeconds((double)Runtime.TimeSinceLastRun.TotalSeconds));
            return Vector3D.Normalize(error) * pidOut + gravity;
        }

        private Vector3D getTargetRotation(Vector3D targetDir, double maxAngleDegrees = 15)
        {
            Vector3D originNormalized = Vector3D.Normalize(controller.WorldMatrix.Down);
            Vector3D toNormalized = Vector3D.Normalize(targetDir);
            double dt = (double)Runtime.TimeSinceLastRun.TotalSeconds;
            Vector3D axis = Vector3D.Cross(originNormalized, toNormalized);
            double sin = axis.Length();
            double cos = Vector3D.Dot(originNormalized, toNormalized);
            double angle = Math.Atan2(sin, cos);
            double maxAngle = MathHelper.ToRadians(maxAngleDegrees);
            angle = MathHelper.Min(angle, maxAngle);
            float pidOut = (float)rotationPID.Calculate(0, angle, TimeSpan.FromSeconds(dt));
            axis = Vector3D.Normalize(axis) * pidOut;
            return axis;
        }

        private Vector4D axisAngle(Vector3D targetAcc)
        {
            Vector3D originNormalized = Vector3D.Normalize(controller.WorldMatrix.Down);
            Vector3D toNormalized = Vector3D.Normalize(targetAcc);
            Vector3D axis = Vector3D.Cross(originNormalized, toNormalized);
            double sin = axis.Length();
            double cos = Vector3D.Dot(originNormalized, toNormalized);
            double angle = Math.Atan2(sin, cos);
            axis = Vector3D.Normalize(axis);
            return new Vector4D(axis, angle);
        }

        private void ThrustOverride(Vector3D desiredAcc)
        {

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

        private void Update()
        {
            var moveIndicator = controller.MoveIndicator;
            var rotationIndicator = controller.RotationIndicator;
            if (moveIndicator.Length() < 0.01 && rotationIndicator.Length() < 0.01)
            {
                Vector3D gravity = controller.GetNaturalGravity();
                // Vector3D targetVectory = TargetVectory(gravity);
                Vector3D desiredAcc = calcAcc(Vector3D.Zero, gravity);
                Vector4D axisAng = axisAngle(desiredAcc + gravity);
                ThrustOverride(desiredAcc);
                Vector3D desiredRot = getTargetRotation(desiredAcc);
                GrayRotate(desiredRot);
                // desiredRot += controller.RotationIndicator.X * controller.WorldMatrix.Down * rotationTarget * Runtime.TimeSinceLastRun.TotalSeconds;
                return;
            }
            else
            {
                speedPID.ResetController();
                rotationPID.ResetController();
                for (int i = 0; i < gyros.Count; i++)
                {
                    gyros[i].GyroOverride = false;
                }
                for (int i = 0; i < thrusters.Count; i++)
                {
                    thrusters[i].ThrustOverride = 0f;
                }
            }
            // 这里添加飞行控制逻辑
            // Vector3D gravity = controller.GetNaturalGravity();
            // Vector3D targetVectory = TargetVectory(gravity);
            // Vector3D desiredAcc = calcAcc(targetVectory * speedTarget, gravity);
            // Vector3D desiredRot = getTargetRotation(desiredAcc + gravity);
            // desiredRot += controller.RotationIndicator.X * controller.WorldMatrix.Down * rotationTarget * Runtime.TimeSinceLastRun.TotalSeconds;
            // GrayRotate(desiredRot);
        }

        private void GrayRotate(Vector3 rotationVector)
        {
            foreach (var gyro in gyros)
            {
                // 世界到陀螺仪本地矩阵
                MatrixD worldToLocal = MatrixD.Transpose(gyro.WorldMatrix);

                // 转换旋转误差到 Gyro 本地坐标
                Vector3D localError = Vector3D.TransformNormal(rotationVector, worldToLocal);

                // 开启 Gyro 脚本控制
                gyro.GyroOverride = true;

                gyro.Pitch = (float)localError.X;
                gyro.Yaw = (float)localError.Y;
                gyro.Roll = (float)localError.Z;
            }
        }
    }
}
