using System;
using System.Collections;
using System.Linq;
using System.Numerics;
using Celeste.Core.Mathematics;
using Celeste.Game.Execution.Startup;
using Celeste.Game.Content.Maps;
using Celeste.Scene.Entities;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Graphics.Content.Tiles;
using Celeste.Graphics.Content.Tiles.Autotiling;
using Celeste.Mods.FurryHelper.Runtime;
using Celeste.Physics.Collision;
using Celeste.Physics.Collision.Shapes;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Tiles;
using Celeste.Simulation.Presentation.Lighting;

namespace Celeste.Mods.FurryHelper.Entities.Walls
{
    [RuntimeTypeAlias("Celeste.Mod.FurryHelper.GlitchWall")]
    [CustomEntity("FurryHelper/GlitchWall = Create")]
    public class GlitchWall : SourceSolidEntityState
    {
        private static readonly Vector2 ScreenSize = new(320f, 180f);
        private static readonly char[] Separators = { ',' };
        private readonly ISourceEngineContext engine;
        private readonly string playerTrackerKey;
        private readonly float[] TimeDelays;
        private readonly char TileType;
        private float timer;
        private bool atEnd;
        private Vector2 StartPos;
        private Vector2 EndPos;
        private TileGrid StartTile;
        private TileGrid EndTile;
        private TileInterceptorState StartInterceptor;
        private TileInterceptorState EndInterceptor;

        private Vector2 LevelTopLeft
        {
            get
            {
                var bounds = ((OriginalLevelSceneRoot)Scene).Bounds;
                return new(bounds.Left, bounds.Top);
            }
        }

        private Vector2 LevelBotRight
        {
            get
            {
                var bounds = ((OriginalLevelSceneRoot)Scene).Bounds;
                return new(bounds.Right, bounds.Bottom);
            }
        }

        public static GlitchWall Create(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
            OriginalLevelEntityData data) => new(data, offset, level.SolidWorldFactory.Create(offset, true));

        public GlitchWall(EntityData data, Vector2 offset, ISourceSolidWorld world)
            : base(offset, data.Width, data.Height, true, world)
        {
            engine = world.EngineContext;
            playerTrackerKey = ((ISourceSceneCatalogEnvironment)engine).SceneCatalog.Find(typeof(RoomPlayerSession))
                .Name;
            TileType = data.Char("tiletype", 'm');
            StartPos = data.Position + offset;
            EndPos = data.Nodes[0] + offset;
            Position = data.Position + offset;
            Tags = ((IOriginalGameTagsEnvironment)engine).Tags.PauseUpdate.Value;
            float beatsPerSecond = data.Int("BPM", 120) / 60f;
            var delays = data.Attr("TimeDelays", "1").Split(Separators, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()).ToArray();
            TimeDelays = new float[delays.Length];
            for (int i = 0; i < delays.Length; i++) TimeDelays[i] = float.Parse(delays[i]) / beatsPerSecond;
        }

        public override void Awake(object scene)
        {
            base.Awake(scene);
            StartTile = getCurrentTilegrid();
            StartInterceptor = new TileInterceptorState(StartTile, true);
            Position = EndPos;
            EndTile = getCurrentTilegrid();
            // The installed body targets StartTile here as well; preserve its interception behavior.
            EndInterceptor = new TileInterceptorState(StartTile, true);
            Position = StartPos;
            Add(StartTile);
            Add(StartInterceptor);
            Add(new LightOccludeState(1f));
            Add(new SimulationCoroutine(engine, DoPattern(), true));
        }

        public TileGrid getCurrentTilegrid()
        {
            var level = (OriginalLevelSceneRoot)Scene;
            var tileBounds = ((OriginalChapterMapData)level.Session.MapData).TileBounds;
            int x = (int)(X / 8f) - tileBounds.Left;
            int y = (int)(Y / 8f) - tileBounds.Top;
            var grid = ((ISourceAutotilingResources)engine).ForegroundAutotiler
                .GenerateOverlay(TileType, x, y, (int)Width / 8, (int)Height / 8, level.SolidsData).TileGrid;
            Depth = -10501;
            return grid;
        }

        public void SetStartGrid()
        {
            Remove(EndTile);
            Remove(EndInterceptor);
            Add(StartTile);
            Add(StartInterceptor);
        }

        public void SetEndGrid()
        {
            Remove(StartTile);
            Remove(StartInterceptor);
            Add(EndTile);
            Add(EndInterceptor);
        }

        public override void Update()
        {
            base.Update();
            if (!((OriginalLevelSceneRoot)Scene).Paused) timer -= engine.Frame.DeltaTime;
        }

        private IEnumerator DoPattern()
        {
            int counter = 0;
            float sCounter = 0f;
            var level = (OriginalLevelSceneRoot)Scene;
            var playerOffset = ScreenSize / 2f;
            var playerOffsetEnd = playerOffset;
            var playerOffsetStart = playerOffset;
            while (true)
            {
                if (TimeDelays[counter] == 0f)
                {
                    counter = (counter + 1) % TimeDelays.Length;
                    continue;
                }

                timer = TimeDelays[counter];
                while (timer > 0f) yield return null;
                sCounter += TimeDelays[counter];
                counter = (counter + 1) % TimeDelays.Length;
                if (TimeDelays[counter] == 0f) continue;
                var player = GetPlayerRider();
                if (atEnd)
                {
                    if (player != null)
                    {
                        playerOffsetEnd = player.Position - level.Camera.Position;
                        player.Position += StartPos - Position;
                    }

                    Position = StartPos;
                    playerOffset = playerOffsetStart;
                    SetStartGrid();
                }
                else
                {
                    if (player != null)
                    {
                        playerOffsetStart = player.Position - level.Camera.Position;
                        player.Position += EndPos - Position;
                    }

                    Position = EndPos;
                    playerOffset = playerOffsetEnd;
                    SetEndGrid();
                }

                if (player != null)
                {
                    var cameraPosition = player.Position - playerOffset;
                    cameraPosition = Vector2Clamping.Clamp(cameraPosition, LevelTopLeft.X, LevelTopLeft.Y,
                        LevelBotRight.X - ScreenSize.X, LevelBotRight.Y - ScreenSize.Y);
                    level.Camera.Position = cameraPosition;
                }

                player = SourceTrackedEntityCollision.FirstAt<RoomPlayerSession>(
                    ((ISourceScene)Scene).SourceScene.Entities, playerTrackerKey, this, Position);
                if (player != null && player.Collider is HitboxColliderGeometry playerHitbox && !IsDreamDashing(player))
                {
                    var direction = IntersectionDiff((HitboxColliderGeometry)Collider, playerHitbox);
                    if (Math.Max(Math.Abs(direction.X), Math.Abs(direction.Y)) <= 8f) player.Position += direction;
                    else
                    {
                        direction = Vector2Math.Normalize(direction);
                        player.Die(direction, false, true);
                    }

                    direction = default;
                }

                atEnd = !atEnd;
            }
        }

        private bool IsDreamDashing(RoomPlayerSession player)
        {
            bool vanilla = player.Machine.State == 9;
            bool communal = player.Machine.State == FurryRuntime.For(engine).DreamTunnelDashState;
            return vanilla || communal;
        }

        public Vector2 IntersectionDiff(HitboxColliderGeometry first, HitboxColliderGeometry second)
        {
            if (!first.Collide(second)) return Vector2.Zero;
            float top = first.AbsoluteTop - second.AbsoluteBottom;
            float bottom = first.AbsoluteBottom - second.AbsoluteTop;
            float left = first.AbsoluteLeft - second.AbsoluteRight;
            float right = first.AbsoluteRight - second.AbsoluteLeft;
            var result = new Vector2(Math.Abs(left) > Math.Abs(right) ? right : left,
                Math.Abs(top) > Math.Abs(bottom) ? bottom : top);
            if (Math.Abs(result.X) < Math.Abs(result.Y)) result.Y = 0f;
            else result.X = 0f;
            return result;
        }

        public RoomPlayerSession GetPlayerSliding()
        {
            foreach (var entity in ((ISourceScene)Scene).SourceScene.Entities.GetTrackedEntities(
                         typeof(RoomPlayerSession)))
            {
                var player = (RoomPlayerSession)entity;
                if (player.Data.Facing == -1 && SourceEntityCollision.CheckAt(this, player, Position + Vector2.UnitX))
                    return player;
                if (player.Data.Facing == 1 && SourceEntityCollision.CheckAt(this, player, Position - Vector2.UnitX))
                    return player;
            }

            return null;
        }
    }
}
