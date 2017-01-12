namespace MoonRiven
{
    using System;
    using LeagueSharp;
    using LeagueSharp.Common;
    using System.Collections.Generic;
    using SharpDX;
    using System.Linq;
    using Color = System.Drawing.Color;

    public static class Orbwalking
    {
        private static readonly string[] NoAttacks =
        {
            "volleyattack", "volleyattackwithsound",
            "jarvanivcataclysmattack", "monkeykingdoubleattack", "shyvanadoubleattack", "shyvanadoubleattackdragon",
            "zyragraspingplantattack", "zyragraspingplantattack2", "zyragraspingplantattackfire",
            "zyragraspingplantattack2fire", "viktorpowertransfer", "sivirwattackbounce", "asheqattacknoonhit",
            "elisespiderlingbasicattack", "heimertyellowbasicattack", "heimertyellowbasicattack2",
            "heimertbluebasicattack", "annietibbersbasicattack", "annietibbersbasicattack2",
            "yorickdecayedghoulbasicattack", "yorickravenousghoulbasicattack", "yorickspectralghoulbasicattack",
            "malzaharvoidlingbasicattack", "malzaharvoidlingbasicattack2", "malzaharvoidlingbasicattack3",
            "kindredwolfbasicattack"
        };

        private static readonly string[] Attacks =
        {
            "caitlynheadshotmissile", "frostarrow", "garenslash2",
            "kennenmegaproc", "masteryidoublestrike", "quinnwenhanced", "renektonexecute", "renektonsuperexecute",
            "rengarnewpassivebuffdash", "trundleq", "xenzhaothrust", "xenzhaothrust2", "xenzhaothrust3", "viktorqbuff"
        };

        private static int _autoattackCounter;
        private static int Delay;
        public static int LastAATick;
        public static int BrainFarmInt = -100;
        private static int LastAttackCommandT;
        private static int LastMoveCommandT;
        private static bool _missileLaunched;
        private static bool DisableNextAttack;
        public static bool Attack = true;
        public static bool Move = true;
        public static bool isNone = false;
        public static bool isCombo = false;
        public static bool isBurst = false;
        public static bool isHarass = false;
        public static bool isLaneClear = false;
        public static bool isLastHit = false;
        public static bool isFlee = false;
        private static float _minDistance = 400;
        private static AttackableUnit _lastTarget;
        private static Vector3 LastMoveCommandPosition = Vector3.Zero;
        private static List<Obj_AI_Base> MinionListAA = new List<Obj_AI_Base>();
        private static readonly Random _random = new Random(DateTime.Now.Millisecond);
        public delegate void AfterAttackEvenH(AttackableUnit unit, AttackableUnit target);
        public delegate void BeforeAttackEvenH(BeforeAttackEventArgs args);
        public delegate void OnAttackEvenH(AttackableUnit unit, AttackableUnit target);
        public delegate void OnNonKillableMinionH(AttackableUnit minion);
        public delegate void OnTargetChangeH(AttackableUnit oldTarget, AttackableUnit newTarget);
        public static event AfterAttackEvenH AfterAttack;
        public static event BeforeAttackEvenH BeforeAttack;
        public static event OnAttackEvenH OnAttack;
        public static event OnNonKillableMinionH OnNonKillableMinion;
        public static event OnTargetChangeH OnTargetChange;

        static Orbwalking()
        {
            Obj_AI_Base.OnPlayAnimation += OnPlayAnimation;
            Obj_AI_Base.OnProcessSpellCast += OnProcessSpellCast;
            Obj_AI_Base.OnDoCast += OnDoCast;
            Spellbook.OnStopCast += OnStopCast;
        }

        private static void OnPlayAnimation(Obj_AI_Base sender, GameObjectPlayAnimationEventArgs Args)
        {
            if (sender == null || !sender.IsMe || Args.Animation == null)
            {
                return;
            }

            var time = 0;
            var canAttack = false;

            switch (Args.Animation)
            {
                case "c29a362b": //Q1
                    time = 291;
                    canAttack = true;
                    Logic.qStack = 1;
                    Logic.lastQTime = Utils.TickCount;
                    break;
                case "c39a37be": //Q2
                    time = 291;
                    canAttack = true;
                    Logic.qStack = 2;
                    Logic.lastQTime = Utils.TickCount;
                    break;
                case "c49a3951": //Q3
                    time = 391;
                    canAttack = true;
                    Logic.qStack = 0;
                    Logic.lastQTime = Utils.TickCount;
                    break;
                case "Spell2": //W
                    time = 50;
                    canAttack = false;
                    break;
                case "Spell3": //E
                    time = 0;
                    canAttack = true;
                    break;
                case "bea6fbd2": //R1
                    time = 50;
                    canAttack = true;
                    break;
                case "bda6fa3f": //R2
                    time = 180;
                    canAttack = true;
                    break;
                default:
                    time = 0;
                    canAttack = true;
                    break;
            }

            if (time > 0)
            {
                if (Orbwalker.ManualCancel || !isNone)
                {
                    if (Orbwalker.CalcutalePing)
                    {
                        if (time - Game.Ping > 0)
                        {
                            Utility.DelayAction.Add(time - Game.Ping, () => Cancel(Args.Animation, canAttack));
                        }
                        else
                        {
                            Utility.DelayAction.Add(1, () => Cancel(Args.Animation, canAttack));
                        }
                    }
                    else
                    {
                        Utility.DelayAction.Add(time, () => Cancel(Args.Animation, canAttack));
                    }
                }
            }
        }

        private static void Cancel(string Name, bool canAttack)
        {
            ResetAutoAttackTimer();
            Game.SendEmote(Emote.Dance);

            if (canAttack)
            {
                if (Orbwalker.GetOrbTar != null && !Orbwalker.GetOrbTar.IsDead)
                {
                    Orbwalk(Orbwalker.GetOrbTar, Game.CursorPos);
                }
                else
                {
                    Orbwalk(null, Game.CursorPos);
                }
            }
            else
            {
                Utility.DelayAction.Add(1, () => Attack = false);
                Utility.DelayAction.Add(2, () => ObjectManager.Player.IssueOrder(GameObjectOrder.MoveTo,
                    ObjectManager.Player.Position.Extend(Game.CursorPos, +10)));
                Utility.DelayAction.Add(350 + Game.Ping, () => Attack= true);
            }
        }

        private static void OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs Args)
        {
            if (sender == null || Args.SData == null)
            {
                return;
            }

            var spellName = Args.SData.Name;

            if (!IsAutoAttack(spellName))
            {
                return;
            }

            if (sender.IsMe &&
                (Args.Target is Obj_AI_Base || Args.Target is Obj_BarracksDampener || Args.Target is Obj_HQ))
            {
                LastAATick = Utils.GameTimeTickCount - Game.Ping / 2;
                _missileLaunched = false;
                LastMoveCommandT = 0;
                _autoattackCounter++;

                var spell = Args.Target as Obj_AI_Base;

                if (spell != null)
                {
                    var target = spell;

                    if (target.IsValid)
                    {
                        FireOnTargetSwitch(target);
                        _lastTarget = target;
                    }
                }
            }

            FireOnAttack(sender, _lastTarget);
        }

        private static void OnDoCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs Args)
        {
            if (sender.IsMe)
            {
                var ping = Game.Ping;

                if (ping <= 30)
                {
                    Utility.DelayAction.Add(30 - ping, () => OnDoCast_Delayed(sender, Args));
                    return;
                }

                OnDoCast_Delayed(sender, Args);
            }
        }

        private static void OnDoCast_Delayed(AttackableUnit sender, GameObjectProcessSpellCastEventArgs Args)
        {
            if (sender == null || Args.SData == null)
            {
                return;
            }

            if (IsAutoAttack(Args.SData.Name))
            {
                FireAfterAttack(sender, Args.Target as AttackableUnit);
                _missileLaunched = true;
            }
        }

        private static void OnStopCast(Spellbook spellbook, SpellbookStopCastEventArgs Args)
        {
            if (spellbook.Owner.IsValid && spellbook.Owner.IsMe && Args.DestroyMissile && Args.StopAnimation)
            {
                ResetAutoAttackTimer();
            }
        }

        private static void FireBeforeAttack(AttackableUnit target)
        {
            if (BeforeAttack != null)
            {
                BeforeAttack(new BeforeAttackEventArgs { Target = target });
            }
            else
            {
                DisableNextAttack = false;
            }
        }

        private static void FireAfterAttack(AttackableUnit unit, AttackableUnit target)
        {
            if (AfterAttack != null && target.IsValidTarget())
            {
                AfterAttack(unit, target);
            }
        }

        private static void FireOnAttack(AttackableUnit unit, AttackableUnit target)
        {
            OnAttack?.Invoke(unit, target);
        }

        private static void FireOnNonKillableMinion(AttackableUnit minion)
        {
            OnNonKillableMinion?.Invoke(minion);
        }

        private static void FireOnTargetSwitch(AttackableUnit newTarget)
        {
            if (OnTargetChange != null && (!_lastTarget.IsValidTarget() || _lastTarget != newTarget))
            {
                OnTargetChange(_lastTarget, newTarget);
            }
        }

        public static bool CanAttack()
        {
            return Utils.GameTimeTickCount + Game.Ping / 2 + 25 >= LastAATick + ObjectManager.Player.AttackDelay * 1000;
        }

        public static bool CanMove(float extraWindup, bool disableMissileCheck = false)
        {
            if (_missileLaunched && Orbwalker.MissileCheck && !disableMissileCheck)
            {
                return true;
            }

            return Utils.GameTimeTickCount + Game.Ping / 2
                   >= LastAATick + ObjectManager.Player.AttackCastDelay * 1000 + extraWindup;
        }

        public static void MoveTo(Vector3 position, float holdAreaRadius = 0, bool overrideTimer = false,
            bool randomizeMinDistance = true)
        {
            var playerPosition = ObjectManager.Player.ServerPosition;

            if (playerPosition.Distance(position, true) < holdAreaRadius * holdAreaRadius)
            {
                if (ObjectManager.Player.Path.Length > 0)
                {
                    ObjectManager.Player.IssueOrder(GameObjectOrder.Stop, playerPosition);
                    LastMoveCommandPosition = playerPosition;
                    LastMoveCommandT = Utils.GameTimeTickCount - 70;
                }

                return;
            }

            var point = position;

            if (ObjectManager.Player.Distance(point, true) < 150 * 150)
            {
                point = playerPosition.Extend(
                    position,
                    randomizeMinDistance ? (_random.NextFloat(0.6f, 1) + 0.2f) * _minDistance : _minDistance);
            }

            var angle = 0f;
            var currentPath = ObjectManager.Player.GetWaypoints();

            if (currentPath.Count > 1 && currentPath.PathLength() > 100)
            {
                var movePath = ObjectManager.Player.GetPath(point);

                if (movePath.Length > 1)
                {
                    var v1 = currentPath[1] - currentPath[0];
                    var v2 = movePath[1] - movePath[0];

                    angle = v1.AngleBetween(v2.To2D());

                    var distance = movePath.Last().To2D().Distance(currentPath.Last(), true);

                    if ((angle < 10 && distance < 500 * 500) || distance < 50 * 50)
                    {
                        return;
                    }
                }
            }

            if (Utils.GameTimeTickCount - LastMoveCommandT < 70 + Math.Min(60, Game.Ping) && !overrideTimer
                && angle < 60)
            {
                return;
            }

            if (angle >= 60 && Utils.GameTimeTickCount - LastMoveCommandT < 60)
            {
                return;
            }

            ObjectManager.Player.IssueOrder(GameObjectOrder.MoveTo, point);
            LastMoveCommandPosition = point;
            LastMoveCommandT = Utils.GameTimeTickCount;
        }

        public static void Orbwalk(AttackableUnit target, Vector3 position, float extraWindup = 90,
            float holdAreaRadius = 0, bool randomizeMinDistance = true)
        {
            if (Utils.GameTimeTickCount - LastAttackCommandT < 70 + Math.Min(60, Game.Ping))
            {
                return;
            }

            if (target.IsValidTarget() && CanAttack() && Attack)
            {
                DisableNextAttack = false;
                FireBeforeAttack(target);

                if (!DisableNextAttack)
                {
                    _missileLaunched = false;

                    if (ObjectManager.Player.IssueOrder(GameObjectOrder.AttackUnit, target))
                    {
                        LastAttackCommandT = Utils.GameTimeTickCount;
                        _lastTarget = target;
                    }

                    return;
                }
            }

            if (CanMove(extraWindup) && Move)
            {
                MoveTo(position, Math.Max(holdAreaRadius, 30), false, randomizeMinDistance);
            }
        }

        public static float GetAttackRange(Obj_AI_Hero target)
        {
            return target.AttackRange + target.BoundingRadius;
        }

        public static Vector3 GetLastMovePosition()
        {
            return LastMoveCommandPosition;
        }

        public static float GetLastMoveTime()
        {
            return LastMoveCommandT;
        }

        public static float GetMyProjectileSpeed()
        {
            return float.MaxValue;
        }

        public static float GetRealAutoAttackRange(AttackableUnit target)
        {
            var result = ObjectManager.Player.AttackRange + ObjectManager.Player.BoundingRadius;

            if (target.IsValidTarget())
            {
                return result + target.BoundingRadius;
            }

            return result;
        }

        public static bool InAutoAttackRange(AttackableUnit target)
        {
            if (!target.IsValidTarget())
            {
                return false;
            }

            var myRange = GetRealAutoAttackRange(target);
            var hero = target as Obj_AI_Hero;

            if (hero != null)
            {
                return
                    Vector2.DistanceSquared(Prediction.GetPrediction(hero, 0).CastPosition.To2D(),
                        ObjectManager.Player.Position.To2D()) <= myRange*myRange;
            }

            var basetarget = target as Obj_AI_Base;

            return
                Vector2.DistanceSquared(
                    basetarget?.ServerPosition.To2D() ?? target.Position.To2D(),
                    ObjectManager.Player.ServerPosition.To2D()) <= myRange*myRange;
        }

        public static bool IsAutoAttack(string name)
        {
            return (name.ToLower().Contains("attack") && !NoAttacks.Contains(name.ToLower()))
                   || Attacks.Contains(name.ToLower());
        }

        public static void ResetAutoAttackTimer()
        {
            LastAATick = 0;
        }

        public static void SetMinimumOrbwalkDistance(float d)
        {
            _minDistance = d;
        }

        public static void SetMovementDelay(int delay)
        {
            Delay = delay;
        }

        public enum OrbwalkingMode
        {
            Combo,
            Mixed,
            LaneClear,
            LastHit,
            Burst,
            Flee,
            None
        }

        public class BeforeAttackEventArgs : EventArgs
        {
            public AttackableUnit Target;
            public Obj_AI_Base Unit = ObjectManager.Player;
            private bool _process = true;

            public bool Process
            {
                get
                {
                    return _process;
                }
                set
                {
                    DisableNextAttack = !value;
                    _process = value;
                }
            }
        }

        public sealed class Orbwalker
        {
            private const float LaneClearWaitTimeMod = 2f;
            private static Menu _config;
            private readonly Obj_AI_Hero Player;
            private Obj_AI_Base _forcedTarget;
            private OrbwalkingMode _mode = OrbwalkingMode.None;
            private Vector3 _orbwalkingPoint;
            private Obj_AI_Minion _prevMinion;
            private static readonly List<Orbwalker> Instances = new List<Orbwalker>();

            public Orbwalker(Menu attachToMenu)
            {
                _config = attachToMenu;

                var priorizeMenu = _config.AddSubMenu(new Menu("Priorize", "Priorize"));
                {
                    priorizeMenu.AddItem(new MenuItem("PriorizeFarm", "Priorize Farm").SetValue(true));
                    priorizeMenu.AddItem(new MenuItem("AttackWards", "Attack Wards").SetValue(true));
                    priorizeMenu.AddItem(new MenuItem("AttackPetsnTraps", "Attack Pets and Traps").SetValue(true));
                    priorizeMenu.AddItem(new MenuItem("AttackGPBarrel", "Attack GPBarrel").SetValue(true));
                    priorizeMenu.AddItem(new MenuItem("Smallminionsprio", "Clear Priorize Small minions").SetValue(false));
                }

                var animationMenu = _config.AddSubMenu(new Menu("Animation", "Animation"));
                {
                    animationMenu.AddItem(new MenuItem("manualCancel", "Semi Cancel Animation").SetValue(true));
                    animationMenu.AddItem(new MenuItem("manualCancelPing", "Cancel Animation Calculate Ping?").SetValue(true));
                }

                var miscMenu = _config.AddSubMenu(new Menu("Misc", "Misc"));
                {
                    miscMenu.AddItem(new MenuItem("MissileCheck", "Missile Check").SetValue(false));
                    miscMenu.AddItem(
                        new MenuItem("HoldPosRadius", "Hold Pos Radius").SetValue(new Slider(0, 0, 250)));
                    miscMenu.AddItem(
                        new MenuItem("ExtraWindup", "Extra Windup").SetValue(new Slider(80, 0, 200)));
                    miscMenu.AddItem(new MenuItem("FarmDelay", "Farm Delay").SetValue(new Slider(0, 0, 200)));
                }

                var drawMenu = _config.AddSubMenu(new Menu("Drawings", "Drawings"));
                {
                    drawMenu.AddItem(
                        new MenuItem("AACircle", "Attack Range").SetValue(new Circle(true, Color.FromArgb(3, 253, 241))));
                    drawMenu.AddItem(
                        new MenuItem("HoldZone", "Hold Zone").SetValue(new Circle(false, Color.FromArgb(3, 253, 241))));
                    drawMenu.AddItem(new MenuItem("AALineWidth", "Circle Width")).SetValue(new Slider(1, 1, 6));
                }

                var keyMenu = _config.AddSubMenu(new Menu("Keys", "Keys"));
                {
                    keyMenu.AddItem(
                        new MenuItem("Burst", "Burst").SetValue(new KeyBind('T', KeyBindType.Press)));
                    keyMenu.AddItem(
                        new MenuItem("Orbwalk", "Combo").SetValue(new KeyBind(32, KeyBindType.Press)));
                    keyMenu.AddItem(new MenuItem("Farm", "Harass").SetValue(new KeyBind('C', KeyBindType.Press)));
                    keyMenu.AddItem(
                        new MenuItem("LaneClear", "Clear").SetValue(new KeyBind('V', KeyBindType.Press)));
                    keyMenu.AddItem(
                        new MenuItem("LastHit", "LastHit").SetValue(new KeyBind('X', KeyBindType.Press)));
                    keyMenu.AddItem(
                        new MenuItem("Flee", "Flee").SetValue(new KeyBind('Z', KeyBindType.Press)));
                }

                Move = true;

                Player = ObjectManager.Player;
                Game.OnUpdate += OnUpdate;
                Drawing.OnDraw += OnDraw;
                Instances.Add(this);
            }

            internal static int FarmDelay => _config.Item("FarmDelay").GetValue<Slider>().Value;

            internal static bool MissileCheck => _config.Item("MissileCheck").GetValue<bool>();

            internal static bool ManualCancel => _config.Item("manualCancel").GetValue<bool>();

            internal static bool CalcutalePing => _config.Item("manualCancelPing").GetValue<bool>();

            internal static AttackableUnit GetOrbTar { get; set; }

            public OrbwalkingMode ActiveMode
            {
                get
                {
                    if (_mode != OrbwalkingMode.None)
                    {
                        return _mode;
                    }

                    if (_config.Item("Orbwalk").GetValue<KeyBind>().Active)
                    {
                        return OrbwalkingMode.Combo;
                    }

                    if (_config.Item("LaneClear").GetValue<KeyBind>().Active)
                    {
                        return OrbwalkingMode.LaneClear;
                    }

                    if (_config.Item("Farm").GetValue<KeyBind>().Active)
                    {
                        return OrbwalkingMode.Mixed;
                    }

                    if (_config.Item("LastHit").GetValue<KeyBind>().Active)
                    {
                        return OrbwalkingMode.LastHit;
                    }

                    if (_config.Item("Burst") != null && _config.Item("Burst").GetValue<KeyBind>().Active)
                    {
                        return OrbwalkingMode.Burst;
                    }

                    if (_config.Item("Flee") != null && _config.Item("Flee").GetValue<KeyBind>().Active)
                    {
                        return OrbwalkingMode.Flee;
                    }

                    return OrbwalkingMode.None;
                }
                set
                {
                    _mode = value;
                }
            }

            public void ForceTarget(Obj_AI_Base target)
            {
                _forcedTarget = target;
            }

            public void SetAttack(bool b)
            {
                Attack = b;
            }

            public void SetMovement(bool b)
            {
                Move = b;
            }

            public void SetOrbwalkingPoint(Vector3 point)
            {
                _orbwalkingPoint = point;
            }

            private void OnUpdate(EventArgs args)
            {
                orbwalkerMode();

                if (ActiveMode == OrbwalkingMode.None)
                {
                    return;
                }

                if (Player.IsCastingInterruptableSpell(true))
                {
                    return;
                }

                MinionListAA = MinionCache.GetMinions(Player.Position, 0);

                var target = GetTarget();
                GetOrbTar = target;
                Orbwalk(
                    target, _orbwalkingPoint.To2D().IsValid() ? _orbwalkingPoint : Game.CursorPos,
                    _config.Item("ExtraWindup").GetValue<Slider>().Value,
                    Math.Max(_config.Item("HoldPosRadius").GetValue<Slider>().Value, 30));
            }

            private void orbwalkerMode()
            {
                isNone = ActiveMode == OrbwalkingMode.None;
                isCombo = ActiveMode == OrbwalkingMode.Combo;
                isBurst = ActiveMode == OrbwalkingMode.Burst;
                isHarass = ActiveMode == OrbwalkingMode.Mixed;
                isLaneClear = ActiveMode == OrbwalkingMode.LaneClear;
                isLastHit = ActiveMode == OrbwalkingMode.LastHit;
                isFlee = ActiveMode == OrbwalkingMode.Flee;
            }

            public AttackableUnit GetTarget()
            {
                AttackableUnit result = null;
                var mode = ActiveMode;

                if (ActiveMode == OrbwalkingMode.Flee)
                {
                    return null;
                }

                if (_forcedTarget.IsValidTarget() && InAutoAttackRange(_forcedTarget))
                {
                    return _forcedTarget;
                }

                if ((mode == OrbwalkingMode.Mixed || mode == OrbwalkingMode.LaneClear)
                    && !_config.Item("PriorizeFarm").GetValue<bool>())
                {
                    var target = TargetSelector.GetTarget(-1, TargetSelector.DamageType.Physical);

                    if (target != null && InAutoAttackRange(target))
                    {
                        return target;
                    }
                }

                if (_config.Item("AttackGPBarrel").GetValue<bool>() &&
                    ObjectManager.Player.ChampionName != "Gangplank")
                {
                    var enemyGangPlank =
                        HeroManager.Enemies.FirstOrDefault(
                            e => e.ChampionName.Equals("gangplank", StringComparison.InvariantCultureIgnoreCase));

                    if (enemyGangPlank != null)
                    {
                        var barrels =
                            ObjectManager.Get<Obj_AI_Minion>()
                                .Where(
                                    minion =>
                                    minion.Team == GameObjectTeam.Neutral
                                    && minion.CharData.BaseSkinName == "gangplankbarrel" && minion.IsHPBarRendered
                                    && minion.IsValidTarget() && InAutoAttackRange(minion));

                        foreach (var barrel in barrels)
                        {
                            if (barrel.Health <= 1f)
                            {
                                return barrel;
                            }

                            var t = (int) (Player.AttackCastDelay*1000) + Game.Ping/2
                                    + 1000*(int) Math.Max(0, Player.Distance(barrel) - Player.BoundingRadius)
                                    /(int) GetMyProjectileSpeed();

                            var barrelBuff =
                                barrel.Buffs.FirstOrDefault(
                                    b =>
                                    b.Name.Equals("gangplankebarrelactive", StringComparison.InvariantCultureIgnoreCase));

                            if (barrelBuff != null && barrel.Health <= 2f)
                            {
                                var healthDecayRate = enemyGangPlank.Level >= 13
                                                          ? 0.5f
                                                          : (enemyGangPlank.Level >= 7 ? 1f : 2f);
                                var nextHealthDecayTime = Game.Time < barrelBuff.StartTime + healthDecayRate
                                                              ? barrelBuff.StartTime + healthDecayRate
                                                              : barrelBuff.StartTime + healthDecayRate * 2;

                                if (nextHealthDecayTime <= Game.Time + t/1000f &&
                                    ObjectManager.Get<Obj_GeneralParticleEmitter>()
                                        .Any(
                                            x =>
                                                x.Name == "Gangplank_Base_E_AoE_Red.troy" &&
                                                barrel.Distance(x.Position) < 10))
                                {
                                    return barrel;
                                }
                            }
                        }

                        if (barrels.Any())
                        {
                            return null;
                        }
                    }
                }

                if (mode == OrbwalkingMode.LaneClear || mode == OrbwalkingMode.Mixed || mode == OrbwalkingMode.LastHit)
                {
                    var MinionList =
                        ObjectManager.Get<Obj_AI_Minion>()
                            .Where(minion => minion.IsValidTarget() && InAutoAttackRange(minion))
                            .OrderByDescending(minion => minion.CharData.BaseSkinName.Contains("Siege"))
                            .ThenBy(minion => minion.CharData.BaseSkinName.Contains("Super"))
                            .ThenBy(minion => minion.Health)
                            .ThenByDescending(minion => minion.MaxHealth);

                    foreach (var minion in MinionList)
                    {
                        var t = (int)(Player.AttackCastDelay * 1000) - 100 + Game.Ping / 2
                                + 1000 * (int)Math.Max(0, Player.Distance(minion) - Player.BoundingRadius)
                                / (int)GetMyProjectileSpeed();

                        var predHealth = HealthPrediction.GetHealthPrediction(minion, t, FarmDelay);

                        if (minion.Team != GameObjectTeam.Neutral && ShouldAttackMinion(minion))
                        {
                            var damage = Player.GetAutoAttackDamage(minion, true) - 5;
                            var killable = predHealth <= damage;

                            if (predHealth <= 0)
                            {
                                FireOnNonKillableMinion(minion);
                            }

                            if (killable)
                            {
                                return minion;
                            }
                        }
                    }
                }

                if ((mode == OrbwalkingMode.LaneClear || mode == OrbwalkingMode.Mixed)
                    && (!MinionManager.GetMinions(
                            ObjectManager.Player.Position,
                            GetRealAutoAttackRange(ObjectManager.Player)).Any()))
                {
                    foreach (var turret in
                        ObjectManager.Get<Obj_AI_Turret>().Where(t => t.IsValidTarget() && InAutoAttackRange(t)))
                    {
                        return turret;
                    }

                    foreach (var turret in
                        ObjectManager.Get<Obj_BarracksDampener>()
                            .Where(t => t.IsValidTarget() && InAutoAttackRange(t)))
                    {
                        return turret;
                    }

                    foreach (var nexus in
                        ObjectManager.Get<Obj_HQ>().Where(t => t.IsValidTarget() && InAutoAttackRange(t)))
                    {
                        return nexus;
                    }
                }

                if (mode != OrbwalkingMode.LastHit)
                {
                    if (!ObjectManager.Player.UnderTurret(true) || mode == OrbwalkingMode.Combo)
                    {
                        var target = TargetSelector.GetTarget(-1, TargetSelector.DamageType.Physical);
                        if (target.IsValidTarget() && InAutoAttackRange(target))
                        {
                            return target;
                        }
                    }
                }

                if (mode == OrbwalkingMode.LaneClear || mode == OrbwalkingMode.Mixed)
                {
                    var jminions =
                        ObjectManager.Get<Obj_AI_Minion>()
                            .Where(
                                mob =>
                                mob.IsValidTarget() && mob.Team == GameObjectTeam.Neutral && InAutoAttackRange(mob)
                                && mob.CharData.BaseSkinName != "gangplankbarrel" && mob.Name != "WardCorpse");

                    result = _config.Item("Smallminionsprio").GetValue<bool>()
                                 ? jminions.MinOrDefault(mob => mob.MaxHealth)
                                 : jminions.MaxOrDefault(mob => mob.MaxHealth);

                    if (result != null)
                    {
                        return result;
                    }
                }

                if (mode == OrbwalkingMode.LaneClear || mode == OrbwalkingMode.Mixed || mode == OrbwalkingMode.LastHit)
                {
                    var closestTower =
                        ObjectManager.Get<Obj_AI_Turret>().MinOrDefault(t => t.IsAlly &&
                                                                             (t.Name.Contains("L_03_A") ||
                                                                              t.Name.Contains("L_02_A") ||
                                                                              t.Name.Contains("C_04_A") ||
                                                                              t.Name.Contains("C_05_A") ||
                                                                              t.Name.Contains("R_02_A") ||
                                                                              t.Name.Contains("R_03_A"))
                                                                             && !t.IsDead
                            ? Player.Distance(t, true)
                            : float.MaxValue);

                    if (closestTower != null && Player.Distance(closestTower, true) < 1500 * 1500)
                    {
                        Obj_AI_Minion farmUnderTurretMinion = null;
                        Obj_AI_Minion noneKillableMinion = null;

                        var minions = MinionListAA.Where(minion =>
                                    closestTower.Distance(minion, true) < 900*900)
                                    .OrderByDescending(minion => minion.CharData.BaseSkinName.Contains("Siege"))
                                    .ThenBy(minion => minion.CharData.BaseSkinName.Contains("Super"))
                                    .ThenByDescending(minion => minion.MaxHealth)
                                    .ThenByDescending(minion => minion.Health);

                        if (minions.Any())
                        {
                            var turretMinion =
                                minions.FirstOrDefault(
                                    minion =>
                                        minion is Obj_AI_Minion &&
                                        HealthPrediction.HasTurretAggro((Obj_AI_Minion) minion));

                            if (turretMinion != null)
                            {
                                var hpLeftBeforeDie = 0;
                                var hpLeft = 0;
                                var turretAttackCount = 0;
                                var turretStarTick = HealthPrediction.TurretAggroStartTick(
                                    turretMinion as Obj_AI_Minion);

                                var turretLandTick = turretStarTick + (int) (closestTower.AttackCastDelay*1000)
                                                     + 1000
                                                     *Math.Max(
                                                         0,
                                                         (int)
                                                         (turretMinion.Distance(closestTower)
                                                          - closestTower.BoundingRadius))
                                                     /(int) (closestTower.BasicAttack.MissileSpeed + 70);

                                for (float i = turretLandTick + 50;
                                     i < turretLandTick + 10 * closestTower.AttackDelay * 1000 + 50;
                                     i = i + closestTower.AttackDelay * 1000)
                                {
                                    var time = (int)i - Utils.GameTimeTickCount + Game.Ping / 2;
                                    var predHP =
                                        (int)
                                        HealthPrediction.LaneClearHealthPrediction(turretMinion, time > 0 ? time : 0);

                                    if (predHP > 0)
                                    {
                                        hpLeft = predHP;
                                        turretAttackCount += 1;
                                        continue;
                                    }

                                    hpLeftBeforeDie = hpLeft;
                                    hpLeft = 0;
                                    break;
                                }

                                if (hpLeft == 0 && turretAttackCount != 0 && hpLeftBeforeDie != 0)
                                {
                                    var damage = (int)Player.GetAutoAttackDamage(turretMinion, true) - 5;
                                    var hits = hpLeftBeforeDie / damage;
                                    var timeBeforeDie = turretLandTick
                                                        + (turretAttackCount + 1)
                                                        *(int) (closestTower.AttackDelay*1000)
                                                        - Utils.GameTimeTickCount;
                                    var timeUntilAttackReady = LastAATick + (int) (Player.AttackDelay*1000)
                                                               > Utils.GameTimeTickCount + Game.Ping/2 + 25
                                        ? LastAATick + (int) (Player.AttackDelay*1000)
                                          - (Utils.GameTimeTickCount + Game.Ping/2 + 25)
                                        : 0;
                                    var timeToLandAttack = Player.AttackCastDelay*1000
                                                           + 1000
                                                           *Math.Max(
                                                               0,
                                                               turretMinion.Distance(Player)
                                                               - Player.BoundingRadius)
                                                           /Player.BasicAttack.MissileSpeed;

                                    if (hits >= 1
                                        && hits*Player.AttackDelay*1000 + timeUntilAttackReady
                                        + timeToLandAttack < timeBeforeDie)
                                    {
                                        farmUnderTurretMinion = turretMinion as Obj_AI_Minion;
                                    }
                                    else if (hits >= 1
                                             && hits*Player.AttackDelay*1000 + timeUntilAttackReady
                                             + timeToLandAttack > timeBeforeDie)
                                    {
                                        noneKillableMinion = turretMinion as Obj_AI_Minion;
                                    }
                                }
                                else if (hpLeft == 0 && turretAttackCount == 0 && hpLeftBeforeDie == 0)
                                {
                                    noneKillableMinion = turretMinion as Obj_AI_Minion;
                                }

                                if (ShouldWaitUnderTurret(noneKillableMinion))
                                {
                                    return null;
                                }

                                if (farmUnderTurretMinion != null)
                                {
                                    return farmUnderTurretMinion;
                                }

                                foreach (var minion in
                                    minions.Where(
                                        x =>
                                            x.NetworkId != turretMinion.NetworkId && x is Obj_AI_Minion
                                            && !HealthPrediction.HasMinionAggro((Obj_AI_Minion) x)))
                                {
                                    var playerDamage = (int)Player.GetAutoAttackDamage(minion) - 5;
                                    var turretDamage = (int)closestTower.GetAutoAttackDamage(minion, true) - 5;
                                    var leftHP = (int)minion.Health % turretDamage;

                                    if (leftHP > playerDamage)
                                    {
                                        return minion;
                                    }
                                }

                                var lastminion =
                                    minions.LastOrDefault(
                                        x =>
                                            x.NetworkId != turretMinion.NetworkId && x is Obj_AI_Minion
                                            && !HealthPrediction.HasMinionAggro((Obj_AI_Minion) x));

                                if (lastminion != null && minions.Count() >= 2)
                                {
                                    if (1f/Player.AttackDelay >= 1f
                                        && (int) (turretAttackCount*closestTower.AttackDelay/Player.AttackDelay)
                                        *(Player.GetAutoAttackDamage(lastminion) - 5) > lastminion.Health)
                                    {
                                        return lastminion;
                                    }

                                    if (minions.Count() >= 5 && 1f / Player.AttackDelay >= 1.2)
                                    {
                                        return lastminion;
                                    }
                                }
                            }
                            else
                            {
                                if (ShouldWaitUnderTurret(noneKillableMinion))
                                {
                                    return null;
                                }

                                foreach (var minion in
                                    minions.Where(
                                        x => x is Obj_AI_Minion && !HealthPrediction.HasMinionAggro((Obj_AI_Minion) x)))
                                {
                                    var playerDamage = (int)Player.GetAutoAttackDamage(minion)- 5;
                                    var turretDamage = (int)closestTower.GetAutoAttackDamage(minion, true)- 5;
                                    var leftHP = (int)minion.Health % turretDamage;

                                    if (leftHP > playerDamage)
                                    {
                                        return minion;
                                    }
                                }

                                var lastminion =
                                    minions.LastOrDefault(
                                        x => x is Obj_AI_Minion && !HealthPrediction.HasMinionAggro((Obj_AI_Minion) x));

                                if (lastminion != null && minions.Count() >= 2)
                                {
                                    if (minions.Count() >= 5 && 1f / Player.AttackDelay >= 1.2)
                                    {
                                        return lastminion;
                                    }
                                }
                            }

                            return null;
                        }
                    }
                }

                if (mode == OrbwalkingMode.LaneClear)
                {
                    if (!ShouldWait())
                    {
                        if (_prevMinion.IsValidTarget() && InAutoAttackRange(_prevMinion))
                        {
                            var predHealth = HealthPrediction.LaneClearHealthPrediction(
                                _prevMinion,
                                (int) (Player.AttackDelay*1000*LaneClearWaitTimeMod),
                                FarmDelay);

                            if (predHealth >= 2*(Player.GetAutoAttackDamage(_prevMinion)-5)
                                || Math.Abs(predHealth - _prevMinion.Health) < float.Epsilon)
                            {
                                return _prevMinion;
                            }
                        }

                        result = (from minion in
                                      MinionListAA.Where(ShouldAttackMinion)
                                  let predHealth = HealthPrediction.LaneClearHealthPrediction(minion,
                                                      (int) (Player.AttackDelay*1000*LaneClearWaitTimeMod), FarmDelay)
                                  where predHealth >= 2*(Player.GetAutoAttackDamage(minion) -5)
                                        || Math.Abs(predHealth - minion.Health) < float.Epsilon
                                  select minion).MaxOrDefault(m => m.Health);

                        if (result != null)
                        {
                            _prevMinion = (Obj_AI_Minion)result;
                        }
                    }
                }

                return result;
            }

            private bool ShouldWait()
            {
                return
                    ObjectManager.Get<Obj_AI_Minion>()
                        .Any(
                            minion =>
                                minion.IsValidTarget() && minion.Team != GameObjectTeam.Neutral
                                && InAutoAttackRange(minion) && MinionManager.IsMinion(minion)
                                && HealthPrediction.LaneClearHealthPrediction(
                                    minion,
                                    (int) (Player.AttackDelay*1000*LaneClearWaitTimeMod),
                                    FarmDelay) <= Player.GetAutoAttackDamage(minion) - 5);
            }

            private bool ShouldWaitUnderTurret(Obj_AI_Minion noneKillableMinion)
            {
                return
                    ObjectManager.Get<Obj_AI_Minion>()
                        .Any(
                            minion =>
                                (noneKillableMinion == null || noneKillableMinion.NetworkId != minion.NetworkId)
                                && minion.IsValidTarget() && minion.Team != GameObjectTeam.Neutral
                                && InAutoAttackRange(minion) && MinionManager.IsMinion(minion)
                                && HealthPrediction.LaneClearHealthPrediction(
                                    minion,
                                    (int)
                                    (Player.AttackDelay*1000
                                     + (Player.AttackCastDelay*1000
                                        + 1000*(Player.AttackRange + 2*Player.BoundingRadius)
                                        /Player.BasicAttack.MissileSpeed)),
                                    FarmDelay) <= Player.GetAutoAttackDamage(minion) - 5);
            }

            private bool ShouldAttackMinion(Obj_AI_Base minion)
            {
                if (minion.Name == "WardCorpse" || minion.CharData.BaseSkinName == "jarvanivstandard")
                {
                    return false;
                }

                if (MinionManager.IsWard((Obj_AI_Minion) minion))
                {
                    return _config.Item("AttackWards").IsActive();
                }

                return (_config.Item("AttackPetsnTraps").GetValue<bool>() || MinionManager.IsMinion((Obj_AI_Minion) minion))
                       && minion.CharData.BaseSkinName != "gangplankbarrel";
            }

            private void OnDraw(EventArgs args)
            {
                if (_config.Item("AACircle").GetValue<Circle>().Active)
                {
                    Render.Circle.DrawCircle(
                        Player.Position,
                        GetRealAutoAttackRange(null) + 65,
                        _config.Item("AACircle").GetValue<Circle>().Color,
                        _config.Item("AALineWidth").GetValue<Slider>().Value);
                }

                if (_config.Item("HoldZone").GetValue<Circle>().Active)
                {
                    Render.Circle.DrawCircle(
                        Player.Position,
                        _config.Item("HoldPosRadius").GetValue<Slider>().Value,
                        _config.Item("HoldZone").GetValue<Circle>().Color,
                        _config.Item("AALineWidth").GetValue<Slider>().Value,
                        true);
                }
            }
        }
    }
}
