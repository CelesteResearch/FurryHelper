using System.Numerics;
using Celeste.Core.Reflection.Runtime;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Core.Components;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Everest.Tracking.Attributes;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Execution.Engine.Rendering;
using Celeste.Game.Execution.Levels;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Triggers.Foundation;

namespace Celeste.Mods.FurryHelper.Triggers.Camera
{
    [RuntimeTypeAlias("Celeste.Mod.FurryHelper.MomentumCameraOffsetTrigger")]
    [CustomEntity("FurryHelper/momentumCameraOffsetTrigger = Create")]
    public class MomentumCameraOffsetTrigger : SourceTriggerEntityState
    {
        private readonly ISourceEngineContext engine;
        private readonly MomentumModes momentumMode;
        private readonly float momentumFrom;
        private readonly float momentumTo;
        private readonly bool onlyOnce;
        private readonly bool xOnly;
        private readonly bool yOnly;
        private Vector2 offsetFrom;
        private Vector2 offsetTo;
        private Vector2 prevPos;
        private bool skipFrame;

        public MomentumCameraOffsetTrigger(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration,
                data.Position + offset, data.Width, data.Height,
                ((ISourceExtremaEnvironment)engine).ExtremaArithmetic)
        {
            this.engine = engine;
            offsetFrom = new Vector2(data.Float("offsetXFrom", 0f) * 48f, data.Float("offsetYFrom", 0f) * 32f);
            offsetTo = new Vector2(data.Float("offsetXTo", 0f) * 48f, data.Float("offsetYTo", 0f) * 32f);
            momentumFrom = data.Float("momentumFrom", 0f);
            momentumTo = data.Float("momentumTo", 0f);
            momentumMode = data.Enum("momentumMode", MomentumModes.HorizontalMomentum);
            onlyOnce = data.Bool("onlyOnce", false);
            xOnly = data.Bool("xOnly", false);
            yOnly = data.Bool("yOnly", false);
            prevPos = Vector2.Zero;
        }

        public static MomentumCameraOffsetTrigger Create(OriginalLevelSceneRoot level, OriginalLevelRoomData room,
            Vector2 offset, OriginalLevelEntityData data)
            => new(level.EngineContext, data, offset);

        public override void OnStay(RoomPlayerSession player)
        {
            base.OnStay(player);
            if (engine.Effects.FreezeTimer > 0f)
            {
                skipFrame = true;
                return;
            }

            if (skipFrame)
            {
                skipFrame = false;
                prevPos = player.Position;
                return;
            }

            var level = (OriginalLevelSceneRoot)Scene;
            if (!yOnly)
                level.CameraOffset.X = ScalarMath.Lerp(offsetFrom.X, offsetTo.X, GetMomentumLerp(player),
                    ((ISourceScalarEnvironment)engine).ScalarArithmetic);
            if (!xOnly)
                level.CameraOffset.Y = ScalarMath.Lerp(offsetFrom.Y, offsetTo.Y, GetMomentumLerp(player),
                    ((ISourceScalarEnvironment)engine).ScalarArithmetic);
            prevPos = player.Position;
        }

        public override void OnLeave(RoomPlayerSession player)
        {
            base.OnLeave(player);
            if (onlyOnce) RemoveSelf();
        }

        protected float GetMomentumLerp(RoomPlayerSession player)
        {
            var velocity = player.Position - prevPos;
            velocity *= ((IOriginalEngineDrawEnvironment)engine).EngineDraw.FPS;
            return momentumMode switch
            {
                MomentumModes.HorizontalMomentum => ScalarMath.ClampedMap(velocity.X, momentumFrom, momentumTo, 0f, 1f),
                MomentumModes.VerticalMomentum => ScalarMath.ClampedMap(velocity.Y, momentumFrom, momentumTo, 0f, 1f),
                _ => 1f
            };
        }
    }
}
