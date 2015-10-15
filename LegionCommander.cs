namespace LegionCommander
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Linq;
    using System.Windows.Input;

    using Ensage;
    using Ensage.Common;
    using Ensage.Common.Extensions;

    using SharpDX;
    using SharpDX.Direct3D9;

    using Color = SharpDX.Color;
    using Font = SharpDX.Direct3D9.Font;

    internal class LegionCommander
    {
        #region Static Fields

      
        private static bool loaded;
        private static Hero me;
        private static Font text;

        #endregion

        #region Public Methods and Operators

        public static Vector3 AARangeVec;
        private static readonly List<ParticleEffect> Effects = new List<ParticleEffect>();
        public static float lastAA = 0;
        public static float attackSleepTime = 0;
        public static IEnumerable<Creep> CreepList;
        public static IEnumerable<Creep> FriendlyCreeps;
        public static IEnumerable<Hero> Enemies;
        public static float averageFriendlyCreepDamage;
        public static Dictionary<Creep, creepHealthTick> healthPredict = new Dictionary<Creep, creepHealthTick> { };
        public struct creepHealthTick
        {
            public float health;
            public float lastTick;
            public float dmgPerTick;
        }

        private static double turnTime;
        private static float legioncommanderMissileSpeed = 900;
        private static float legioncommanderAttackDelay = 0.46f;
        private static float legioncommanderBackswing = 0.64f;

        private static Item blinkDag;
        private static Item bMail;
        private static Item armlet;
        private static Item bkb;
        private static Item mjol;

        public static void Init()
        {
            Console.WriteLine(ObjectMgr.LocalHero.Name.ToString());
            if (ObjectMgr.LocalHero.Name.ToString() == "npc_dota_hero_legion_commander")
            {
                Game.OnUpdate += Game_OnUpdate;
                loaded = false;
                text = new Font(
                    Drawing.Direct3DDevice9,
                    new FontDescription
                        {
                            FaceName = "Tahoma",
                            Height = 13,
                            OutputPrecision = FontPrecision.Default,
                            Quality = FontQuality.Default
                        });

                Drawing.OnPreReset += Drawing_OnPreReset;
                Drawing.OnPostReset += Drawing_OnPostReset;
                Drawing.OnEndScene += Drawing_OnEndScene;
                AppDomain.CurrentDomain.DomainUnload += CurrentDomainDomainUnload;
                Game.OnWndProc += Game_OnWndProc;
                Player.OnExecuteOrder += Player_OnExecuteAction;
            }
        }

        #endregion

        #region Methods

        private static void CurrentDomainDomainUnload(object sender, EventArgs e)
        {
            text.Dispose();
        }

        private static void Drawing_OnEndScene(EventArgs args)
        {
            if (Drawing.Direct3DDevice9 == null || Drawing.Direct3DDevice9.IsDisposed || !Game.IsInGame)
            {
                return;
            }

            var player = ObjectMgr.LocalPlayer;
            if (player == null || player.Team == Team.Observer)
            {
                return;
            }

            
        }

        private static void drawAArange()
        {
            if (!Game.IsInGame)
                return;

            foreach (var e in Effects)
            {
                e.Dispose();
            }
            Effects.Clear();

            

            var aaRangeEffect = ObjectMgr.LocalHero.AddParticleEffect(@"particles\ui_mouseactions\range_display.vpcf");
            var Qrange = ObjectMgr.LocalHero.AddParticleEffect(@"particles\ui_mouseactions\range_display.vpcf");
            aaRangeEffect.SetControlPoint(1, new Vector3(ObjectMgr.LocalHero.AttackRange, 0, 0));
            Qrange.SetControlPoint(1, new Vector3(ObjectMgr.LocalHero.Spellbook.SpellQ.CastRange, 0, 0));
            Effects.Add(aaRangeEffect);
            Effects.Add(Qrange);


        }


        private static void Drawing_OnPostReset(EventArgs args)
        {
            text.OnResetDevice();
        }

        private static void Drawing_OnPreReset(EventArgs args)
        {
            text.OnLostDevice();
        }


        private static void Game_OnUpdate(EventArgs args)
        {
            if (!loaded)
            {
                me = ObjectMgr.LocalHero;
                if (!Game.IsInGame || me == null || me.ClassID != ClassID.CDOTA_Unit_Hero_Legion_Commander)
                {
                    return;
                }
                loaded = true;

                Console.WriteLine("Jew legioncommander Loaded");
                armlet = null;
                bMail = null;
                mjol = null;
                bkb = null;
                blinkDag = null;
                drawAArange();
            }

           
                armlet = me.Inventory.Items.FirstOrDefault(x => x.ClassID == ClassID.CDOTA_Item_Armlet);
          

          
                bMail = me.Inventory.Items.FirstOrDefault(x => x.Name == "item_blade_mail");
           

          
                mjol = me.Inventory.Items.FirstOrDefault(x => x.Name == "item_mjollnir");
           

           
                bkb = me.Inventory.Items.FirstOrDefault(x => x.Name == "item_black_king_bar");
            

           
                blinkDag = me.Inventory.Items.FirstOrDefault(x => x.ClassID == ClassID.CDOTA_Item_BlinkDagger);
            

            CreepList = ObjectMgr.GetEntities<Creep>()
                   .Where(
                    minion =>
                                    (minion.Team == ObjectMgr.LocalHero.Team || minion.Team == ObjectMgr.LocalHero.GetEnemyTeam()) && minion.IsAlive && ObjectMgr.LocalHero.Distance2D(minion) <= ObjectMgr.LocalHero.AttackRange * 3)
                                   .OrderByDescending(minion => minion.ClassID == ClassID.CDOTA_BaseNPC_Creep_Siege)
                                   .ThenByDescending(minion => minion.Team == ObjectMgr.LocalHero.Team)
                                   .ThenBy(minion => minion.Health);
                                   //.ThenByDescending(minion => Math.Abs((int)minion.Health - (int)minion.MaximumHealth))
                                   //.ThenBy(minion => minion.Health)
                                   //.ThenByDescending(minion => minion.MaximumHealth);

            FriendlyCreeps = ObjectMgr.GetEntities<Creep>()
                  .Where(
                   minion =>
                                  minion.Team == ObjectMgr.LocalHero.Team && ObjectMgr.LocalHero.Distance2D(minion) <= ObjectMgr.LocalHero.AttackRange * 3);
           
            Enemies = ObjectMgr.GetEntities<Hero>()
                .Where(
                 EnemyHero =>
                                EnemyHero.IsValidTarget() && ObjectMgr.LocalHero.Distance2D(EnemyHero) <= ObjectMgr.LocalHero.AttackRange)
                                .OrderBy(EnemyHero => EnemyHero.Health);
            
            foreach (Creep c in CreepList)
            {
                if (!healthPredict.ContainsKey(c))
                {
                    creepHealthTick cht = new creepHealthTick();
                    cht.health = c.Health;
                    cht.lastTick = Game.GameTime;
                    cht.dmgPerTick = 0;
                   
                    healthPredict.Add(c, cht);

                } else {

                    creepHealthTick lastval = healthPredict[c];

                    if (Game.GameTime - lastval.lastTick >= 1)
                    {
                        creepHealthTick newval = new creepHealthTick();

                        newval.health = c.Health;
                        newval.lastTick = Game.GameTime;
                        newval.dmgPerTick = (lastval.health - newval.health);

                        healthPredict[c] = newval;
                    }
                }
            }

           
            
            if (!Game.IsInGame || me == null || me.ClassID != ClassID.CDOTA_Unit_Hero_Legion_Commander)
            {
                loaded = false;
                Console.WriteLine("Jew Legion Commander Unloaded");
                return;
            }

            if (Game.IsPaused)
            {
                return;
            }

        }

        private static void Game_OnWndProc(WndEventArgs args)
        {
           

            if (args.WParam == 120 && Game.IsChatOpen == false)
            {

                foreach (var minion in CreepList)

                {
                    
                    creepHealthTick c = healthPredict[minion];

                    if (canKill(c,minion))
                    {

                        if (Utils.SleepCheck("attackSleep"))
                        {
                            minion.AddParticleEffect("particles/items_fx/aura_shivas.vpcf");
                            attackSleepTime = legioncommanderAttackDelay / (1 + ObjectMgr.LocalHero.AttackSpeedValue);
                            // Console.WriteLine("MY ATTACK SPEED: " + (0.7f / (1 + ObjectMgr.LocalHero.AttackSpeedValue)).ToString() + "    TURN TIME: " + GetTurnTime(minion).ToString());
                            ObjectMgr.LocalHero.Attack(minion);
                            Utils.Sleep((double)attackSleepTime, "attackSleep");
                            break;
                        }
                    }
                }

                if (Utils.SleepCheck("attackSleep"))
                {
                    if (ObjectMgr.LocalHero.Position.Distance(Game.MousePosition) > 100)
                    {
                        ObjectMgr.LocalHero.Move(Game.MousePosition);
                        //Console.WriteLine(ObjectMgr.LocalHero.AttackSpeedValue.ToString());
                    }
                }
               

            }

          
            
        }

        private static bool canKill(creepHealthTick c, Unit u)
        {
            legioncommanderAttackDelay = legioncommanderAttackDelay / (1 + ObjectMgr.LocalHero.AttackSpeedValue);
            var ping2ms = Game.Ping / 1000;
            var tickDmg = c.dmgPerTick;
            var myDamage = ObjectMgr.LocalHero.DamageAverage;
            var bonusDamage = ObjectMgr.LocalHero.BonusDamage;
            var damageWhileTurning = tickDmg * (GetTurnTime(u) + ping2ms);
            var damageWhileWinding = tickDmg * legioncommanderAttackDelay;
            //var damageWhileTravel = (ObjectMgr.LocalHero.Position.Distance(u.Position) / ObjectMgr.LocalHero.MovementSpeed) * tickDmg;

            if (u.DamageTaken(myDamage + bonusDamage + damageWhileTurning + damageWhileWinding, DamageType.Physical, ObjectMgr.LocalHero, false) >= c.health && (damageWhileTurning + damageWhileWinding) < c.health && ObjectMgr.LocalHero.Position.Distance2D(u) < ObjectMgr.LocalHero.AttackRange)
            {
               // Console.WriteLine("HEALTH: " + c.health.ToString() + " " + myDamage + " " + bonusDamage + " " + damageWhileTurning + " " + damageWhileWinding + " " + damageWhileTravel);

                if (u.Team == ObjectMgr.LocalHero.Team)
                {
                    if (c.health < u.MaximumHealth / 2)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return true;
                }
            
            }
            else
            {
                return false;
            }
        }

        private static float GetTurnTime(Unit u)
        {
            return (float)(Math.Max( Math.Abs(me.FindAngleR() - Utils.DegreeToRadian(me.FindAngleBetween(ObjectMgr.LocalHero.Position))) - 0.69,
                            0) / (0.5 * (1 / 0.03)));
        }



        static void Player_OnExecuteAction(Player sender, ExecuteOrderEventArgs args)
        {

            switch (args.Order)
            {

                case Order.AbilityTarget:
                    {
                        var unit = args.Target as Unit;
                        if (unit != null && args.Ability != null)
                            TargetSpellCheck(sender, args);
                        break;
                    }
            }

        }

        static void TargetSpellCheck(Player sender, ExecuteOrderEventArgs args)
        {

            var hero = args.Target as Hero;
            var realTarget = hero;
            if (hero != null)
            {
                // Check if target is illusion and real hero is near
                if (hero.IsIllusion)
                {
                    realTarget = hero.ReplicateFrom;
                } 

                    if (realTarget.IsAlive && realTarget.IsVisible)
                    {

                        if (realTarget.Distance2D(args.Entities.First()) - realTarget.HullRadius < args.Ability.CastRange)
                        {

                            if (armlet != null)
                            {
                                if (!armlet.IsToggled)
                                {
                                    armlet.ToggleAbility();
                                }
                            }
                            if (ObjectMgr.LocalHero.Spellbook.SpellW.CanBeCasted())
                            {
                                ObjectMgr.LocalHero.Spellbook.SpellW.UseAbility(ObjectMgr.LocalHero);
                            }
                            if (bMail != null)
                            {
                                bMail.UseAbility();
                            }
                            if (bkb != null)
                            {
                                bkb.UseAbility();
                            }
                            if (mjol != null)
                            {
                                mjol.UseAbility(ObjectMgr.LocalHero);
                            }

                            args.Ability.UseAbility(realTarget);
                            args.Process = false;
                            return;
                        }
                        else
                        {

                            if (blinkDag != null && blinkDag.CanBeCasted() && blinkDag.Cooldown == 0f && ObjectMgr.LocalHero.Position.Distance2D(realTarget) <= 1200)
                            {

                                if (armlet != null)
                                {
                                    if (!armlet.IsToggled)
                                    {
                                        armlet.ToggleAbility();
                                    }
                                }
                                if (ObjectMgr.LocalHero.Spellbook.SpellW.CanBeCasted())
                                {
                                    ObjectMgr.LocalHero.Spellbook.SpellW.UseAbility(ObjectMgr.LocalHero);
                                }


                                if (bMail != null)
                                {
                                    bMail.UseAbility();
                                }
                                if (bkb != null)
                                {
                                    bkb.UseAbility();
                                }
                                if (mjol != null)
                                {
                                    mjol.UseAbility(ObjectMgr.LocalHero);
                                }

                                blinkDag.UseAbility(realTarget.Position);

                                args.Ability.UseAbility(realTarget);
                                args.Process = false;
                                return;
                        }
                    
                    }
                }
            }
            // Check if target is linkens protected for certain spells
          
        }


        #endregion

    }
}