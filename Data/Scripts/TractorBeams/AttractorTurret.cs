

using System;
using VRage.Game.Components;
using Sandbox.Common.ObjectBuilders;
using VRage.Game;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.ObjectBuilders;
using VRage.Game.ModAPI;
using Sandbox.Game.EntityComponents;
using System.Collections.Generic;
using VRage.ModAPI;
using System.Text;
using VRage;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;
using Sandbox.Game.Entities;
using VRageMath;
using VRage.Game.ModAPI.Interfaces;
using VRage.Game.Entity;
using VRage.Utils;

namespace TractorBeam
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_LargeGatlingTurret), true, new string[] { "LargeTractorBeam", "TractorBeam" })]
    public class TractorBeamTurret : MyGameLogicComponent
    {
        MyDefinitionId electricityDefinition = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Electricity");

        MyObjectBuilder_EntityBase objectBuilder = null;

        MyEntity3DSoundEmitter e;


        IMyCubeBlock cubeBlock = null;
        Sandbox.ModAPI.IMyFunctionalBlock functionalBlock = null;
        Sandbox.ModAPI.IMyTerminalBlock terminalBlock;

        MyResourceSinkComponent resourceSink;
        IMyInventory m_inventory;

        string subtypeName;

        AttractorWeaponInfo attractorWeaponInfo;

        float powerConsumption;
        float setPowerConsumption;

        float currentHeat;
        bool overheated = false;

        long lastShootTime;
        int lastShootTimeTicks;

        // Caching for performance
        private IMyCubeGrid cachedTarget;
        private int targetCacheTicks = 0;
        private int cachedCharges = -1;
        private int chargeCacheTicks = 0;


        bool hitBool = false;

        int ticks = 0;

        int damageUpgrades = 0;
        float heatUpgrades = 0;
        float efficiencyUpgrades = 1f;

        List<MyObjectBuilder_AmmoMagazine> chargeObjectBuilders;
        List<SerializableDefinitionId> chargeDefinitionIds;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            this.objectBuilder = objectBuilder;

            Entity.NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME | MyEntityUpdateEnum.EACH_FRAME | MyEntityUpdateEnum.EACH_100TH_FRAME;

            functionalBlock = Entity as Sandbox.ModAPI.IMyFunctionalBlock;
            cubeBlock = Entity as IMyCubeBlock;
            terminalBlock = Entity as Sandbox.ModAPI.IMyTerminalBlock;

            subtypeName = functionalBlock.BlockDefinition.SubtypeName;

            getAttractorWeaponInfo(subtypeName);
            initCharges();

            cubeBlock.AddUpgradeValue("PowerEfficiency", 1.0f);
            cubeBlock.OnUpgradeValuesChanged += onUpgradeValuesChanged;

            terminalBlock.AppendingCustomInfo += appendCustomInfo;

            IMyCubeBlock cube = Entity as IMyCubeBlock;
            lastShootTime = ((MyObjectBuilder_LargeGatlingTurret)cube.GetObjectBuilderCubeBlock()).GunBase.LastShootTime;

        }

        public override void UpdateBeforeSimulation100()
        {
            if (UI == null)
            {
                UI = new LSE.TractorUI<Sandbox.ModAPI.Ingame.IMyLargeTurretBase>();
                UI.CreateUI((Sandbox.ModAPI.IMyTerminalBlock)Entity);
            }
        }

        public LSE.TractorUI<Sandbox.ModAPI.Ingame.IMyLargeTurretBase> UI;

        private void onUpgradeValuesChanged() {

            if (Entity != null) {

                efficiencyUpgrades = cubeBlock.UpgradeValues["PowerEfficiency"];

            }
        }

        public void appendCustomInfo(Sandbox.ModAPI.IMyTerminalBlock block, StringBuilder info)
        {
            info.Clear();


            info.AppendLine("Type: " + cubeBlock.DefinitionDisplayNameText);
            info.AppendLine("Required Input: " + powerConsumption.ToString("N") + "MW");
            info.AppendLine("Maximum Input: " + attractorWeaponInfo.PowerUsage.ToString("N") + "MW");

            info.AppendLine(" ");

            if (attractorWeaponInfo.Classes > 1) {

                info.AppendLine("Class: " + "Class " + (damageUpgrades + 1) + " Beam Weapon");

            }

            info.AppendLine("Heat: " + currentHeat + "/" + (attractorWeaponInfo.MaxHeat).ToString("N") + "C");
            info.AppendLine("Overheated: " + overheated);
        }

        private void initCharges() {

            chargeObjectBuilders = new List<MyObjectBuilder_AmmoMagazine>();

            if (attractorWeaponInfo.Classes == 1) {

                chargeObjectBuilders.Add(new MyObjectBuilder_AmmoMagazine() { SubtypeName = "" + attractorWeaponInfo.AmmoName });

            } else {

                for (int i = 1; i <= attractorWeaponInfo.Classes; i++) {

                    chargeObjectBuilders.Add(new MyObjectBuilder_AmmoMagazine() { SubtypeName = "" + "Class" + i + attractorWeaponInfo.AmmoName });
                }
            }

            chargeDefinitionIds = new List<SerializableDefinitionId>();

            if (attractorWeaponInfo.Classes == 1) {

                chargeDefinitionIds.Add(new SerializableDefinitionId(typeof(MyObjectBuilder_AmmoMagazine), "" + attractorWeaponInfo.AmmoName));

            } else {

                for (int i = 1; i <= attractorWeaponInfo.Classes; i++) {

                    chargeDefinitionIds.Add(new SerializableDefinitionId(typeof(MyObjectBuilder_AmmoMagazine), "Class" + i + attractorWeaponInfo.AmmoName));
                }
            }
        }

        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return objectBuilder;
        }

        private void getAttractorWeaponInfo(string name) {

            if (subtypeName == "LargeTractorBeam") {
                attractorWeaponInfo = TractorBeamManager.LargeBlockAttractorTurret;
            }
			else if (subtypeName == "TractorBeam") {
                attractorWeaponInfo = TractorBeamManager.LargeBlockAttractorTurret;
            }
        }

        public override void UpdateOnceBeforeFrame()
        {
            if (UI == null)
            {
                UI = new LSE.TractorUI<Sandbox.ModAPI.Ingame.IMyLargeTurretBase>();
                UI.CreateUI((Sandbox.ModAPI.IMyTerminalBlock)Entity);
            }
            resourceSink = Entity.Components.Get<MyResourceSinkComponent>();

            resourceSink.SetRequiredInputByType(electricityDefinition, 0.0021f);
            setPowerConsumption = 0.0081f;

            m_inventory = ((Sandbox.ModAPI.Ingame.IMyTerminalBlock)Entity).GetInventory(0) as IMyInventory;

        }

        public IMyCubeGrid GetTarget()
        {
            //var turretBase = Entity as Sandbox.ModAPI.IMyLargeTurretBase;
            //var fixedWeapon = Entity as Sandbox.ModAPI.IMyUserControllableGun;

            //if (turretBase != null)
            //{
            //    target = turretBase.Target;
            //}

            try {
                MyEntitySubpart subpart1 = cubeBlock.GetSubpart("GatlingTurretBase1");
                MyEntitySubpart subpart2 = subpart1.GetSubpart("GatlingTurretBase2");

                if (cubeBlock == null || cubeBlock.CubeGrid == null || subpart1 == null || subpart2 == null || subpart1.WorldMatrix == null || subpart2.WorldMatrix == null) { return null; }

                var from = subpart2.WorldMatrix.Translation + subpart2.WorldMatrix.Forward * 0.3d;
                var to = subpart2.WorldMatrix.Translation + subpart2.WorldMatrix.Forward * 800d;

                LineD ray = new LineD(from, to);
                List<MyLineSegmentOverlapResult<MyEntity>> result = new List<MyLineSegmentOverlapResult<MyEntity>>();
                MyGamePruningStructure.GetTopmostEntitiesOverlappingRay(ref ray, result, MyEntityQueryType.Both);

                foreach (var resultItem in result)
                {
                    if (resultItem.Element == null) { continue; }

                    if (resultItem.Element.EntityId != cubeBlock.CubeGrid.EntityId)
                    {
                        if (resultItem.Element is IMyCubeGrid)
                        {
                            return resultItem.Element as IMyCubeGrid;
                        }
                    }
                }
            }
            catch (KeyNotFoundException)
            {

            }
            return null;

        }

        public override void UpdateBeforeSimulation()
        {
            if (UI == null || !UI.Initialized_Attractor) { return; }
            if (Entity == null) { return; }

            IMyCubeBlock cube = Entity as IMyCubeBlock;

            // Cache target to reduce raycasting frequency
            if (targetCacheTicks <= 0) {
                cachedTarget = GetTarget();
                targetCacheTicks = 2; // update every 2 frames
            } else {
                targetCacheTicks--;
            }
            var target = cachedTarget;
            

            var beamEnabled = functionalBlock != null && functionalBlock.Enabled;
            var isShooting = beamEnabled && (Entity as Sandbox.ModAPI.IMyUserControllableGun).IsShooting;
            var hasLineOfSight = target != null && target.Physics != null && cubeBlock != null && cubeBlock.CubeGrid != null && target.EntityId != cubeBlock.CubeGrid.EntityId;

            if (!beamEnabled && target != null && target.Physics != null)
            {
                var damping = new Vector3D(target.Physics.LinearVelocity) * -0.15d;
                target.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, damping, null, null);
            }

            if (isShooting && hasLineOfSight)
            {
                var grid = target;
                MyEntitySubpart subpart1 = cubeBlock.GetSubpart("GatlingTurretBase1");
                MyEntitySubpart subpart2 = subpart1.GetSubpart("GatlingTurretBase2");

                if (subpart1 == null || subpart2 == null || subpart1.WorldMatrix == null || subpart2.WorldMatrix == null) { return; }

                var from = subpart2.WorldMatrix.Translation + subpart2.WorldMatrix.Forward * 0.3d;
                var to = target.Physics.CenterOfMassWorld;
                var toTarget = to - from;
                toTarget.Normalize();


                var distance = Vector3D.Distance(from, to);
                // new code to apply force based on distance and eliminate the min max sliders
                var desiredDistance = UI.DistanceSlider.Getter(terminalBlock);
                var force = UI.StrengthSlider.Getter(terminalBlock);
                var forceVector = force * toTarget;

                if (distance > desiredDistance)
                {
                    grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, -forceVector, null, null);
                }
                else if (distance < desiredDistance)
                {
                    grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, forceVector, null, null);
                }
                else
                {
                    var velocity = new Vector3D(grid.Physics.LinearVelocity);
                    velocity.Normalize();
                    grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, force * -velocity / 3, null, null);
                }

                // DrawShootingEffect(from, to);
            }
            Recharge();
        }

        public void DrawShootingEffect(Vector3D from, Vector3D to)
        {
            var maincolor = Color.White.ToVector4();
            var auxcolor = Color.Blue.ToVector4();
            var material = MyStringId.GetOrCompute("WeaponLaser");

            if (!MyAPIGateway.Utilities.IsDedicated)
            {
                if (!MyAPIGateway.Session.CreativeMode)
                {
                    VRage.Game.MySimpleObjectDraw.DrawLine(from, to, material, ref auxcolor, 0.15f * (currentHeat / attractorWeaponInfo.MaxHeat + 0.2f));
                    VRage.Game.MySimpleObjectDraw.DrawLine(from, to, material, ref maincolor, 0.5f * (currentHeat / attractorWeaponInfo.MaxHeat + 0.2f));
                }
                else
                {
                    VRage.Game.MySimpleObjectDraw.DrawLine(from, to, material, ref auxcolor, 0.15f * 1.2f);
                    VRage.Game.MySimpleObjectDraw.DrawLine(from, to, material, ref maincolor, 0.5f * 1.2f);
                }
            }
        }

        void Recharge()
        {
            // Cache inventory check to reduce frequency
            int chargesInInventory;
            if (chargeCacheTicks <= 0) {
                chargesInInventory = (int)m_inventory.GetItemAmount(chargeDefinitionIds[damageUpgrades]);
                cachedCharges = chargesInInventory;
                chargeCacheTicks = 10; // update every 10 frames
            } else {
                chargesInInventory = cachedCharges;
                chargeCacheTicks--;
            }

            if (chargesInInventory < attractorWeaponInfo.KeepAtCharge) {

				if (resourceSink.RequiredInputByType(electricityDefinition) != (attractorWeaponInfo.PowerUsage/efficiencyUpgrades)) {
					
					resourceSink.SetRequiredInputByType (electricityDefinition, (attractorWeaponInfo.PowerUsage/efficiencyUpgrades));

					setPowerConsumption = (attractorWeaponInfo.PowerUsage/efficiencyUpgrades);
					powerConsumption = (attractorWeaponInfo.PowerUsage/efficiencyUpgrades);

				} else {

					if (!functionalBlock.Enabled) {
						
						powerConsumption = 0.0001f;
					}
				}

				if (resourceSink.CurrentInputByType (electricityDefinition) == (attractorWeaponInfo.PowerUsage/efficiencyUpgrades)) {

					if (!overheated) {
						m_inventory.AddItems ((MyFixedPoint)(attractorWeaponInfo.KeepAtCharge - chargesInInventory), chargeObjectBuilders [damageUpgrades]);
					}
				}

			} else if(chargesInInventory > attractorWeaponInfo.KeepAtCharge) {
				
				m_inventory.RemoveItemsOfType ((MyFixedPoint)(chargesInInventory - attractorWeaponInfo.KeepAtCharge), chargeObjectBuilders [damageUpgrades]);

			} else  {
				
				if (setPowerConsumption != 0.0001f) {

					resourceSink.SetRequiredInputByType (electricityDefinition, 0.0001f);

					setPowerConsumption = 0.0001f;
					powerConsumption = 0.0001f;
				}
			}

//            terminalBlock.RefreshCustomInfo ();
		}

		public override void Close ()
		{

			if (m_inventory != null) {

				for (int i = 0; i < attractorWeaponInfo.Classes; i++) { 
					m_inventory.RemoveItemsOfType (m_inventory.GetItemAmount (chargeDefinitionIds[i]), chargeObjectBuilders[i]);
				}
			}

			base.Close ();
		}

		public override void MarkForClose ()
		{
			if (m_inventory != null) {

				for (int i = 0; i < attractorWeaponInfo.Classes; i++) { 
					m_inventory.RemoveItemsOfType (m_inventory.GetItemAmount (chargeDefinitionIds[i]), chargeObjectBuilders[i]);
				}
			}

			base.MarkForClose ();
		}

	}
}

