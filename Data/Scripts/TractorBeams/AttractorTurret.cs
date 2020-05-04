

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
using VRage.Game.Entity;
using Sandbox.Game.Gui;
using VRage.Utils;
using VRage.Game.ModAPI.Interfaces;

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

            Entity.NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME | MyEntityUpdateEnum.EACH_FRAME;

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
            info.AppendLine("Maximum Input: " + attractorWeaponInfo.powerUsage.ToString("N") + "MW");

            info.AppendLine(" ");

            if (attractorWeaponInfo.classes > 1) {

                info.AppendLine("Class: " + "Class " + (damageUpgrades + 1) + " Beam Weapon");

            }

            info.AppendLine("Heat: " + currentHeat + "/" + (attractorWeaponInfo.maxHeat).ToString("N") + "C");
            info.AppendLine("Overheated: " + overheated);
        }

        private void initCharges() {

            chargeObjectBuilders = new List<MyObjectBuilder_AmmoMagazine>();

            if (attractorWeaponInfo.classes == 1) {

                chargeObjectBuilders.Add(new MyObjectBuilder_AmmoMagazine() { SubtypeName = "" + attractorWeaponInfo.ammoName });

            } else {

                for (int i = 1; i <= attractorWeaponInfo.classes; i++) {

                    chargeObjectBuilders.Add(new MyObjectBuilder_AmmoMagazine() { SubtypeName = "" + "Class" + i + attractorWeaponInfo.ammoName });
                }
            }

            chargeDefinitionIds = new List<SerializableDefinitionId>();

            if (attractorWeaponInfo.classes == 1) {

                chargeDefinitionIds.Add(new SerializableDefinitionId(typeof(MyObjectBuilder_AmmoMagazine), "" + attractorWeaponInfo.ammoName));

            } else {

                for (int i = 1; i <= attractorWeaponInfo.classes; i++) {

                    chargeDefinitionIds.Add(new SerializableDefinitionId(typeof(MyObjectBuilder_AmmoMagazine), "Class" + i + attractorWeaponInfo.ammoName));
                }
            }
        }

        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return objectBuilder;
        }

        private void getAttractorWeaponInfo(string name) {

            if (subtypeName == "LargeTractorBeam") {
                attractorWeaponInfo = TractorBeamManager.largeBlockAttractorTurret;
            }
			else if (subtypeName == "TractorBeam") {
                attractorWeaponInfo = TractorBeamManager.largeBlockAttractorTurret;
            }
        }

        public override void UpdateOnceBeforeFrame()
        {

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
            if (UI == null || !UI.Initialized) { return; }
            if (Entity == null) { return; }

            IMyCubeBlock cube = Entity as IMyCubeBlock;
            var target = GetTarget();
            

            var isShooting = (Entity as Sandbox.ModAPI.IMyUserControllableGun).IsShooting;
            if (isShooting && target != null && target.Physics != null)
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
                var min = UI.MinSlider.Getter(terminalBlock);
                var max = UI.MaxSlider.Getter(terminalBlock);
                var force = UI.StrengthSlider.Getter(terminalBlock);
                var forceVector = force * toTarget;

                if (distance > max)
                {
                    grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, -forceVector, null, null);
                }
                else if (distance < min)
                {
                    var percentage = 1 - (distance * distance) / (min * min);
                    var additionalForce = toTarget * percentage * (UI.StrengthSlider.Max - force);
                    var velocity = new Vector3D(grid.Physics.LinearVelocity);
					velocity.Normalize();
                    grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, forceVector + additionalForce / 4, null, null);
                }
                else
                {
                    var velocity = new Vector3D(grid.Physics.LinearVelocity);
                    velocity.Normalize();
                    grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, force * -velocity / 3, null, null);
                }

                DrawShootingEffect(from, to);
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
                    VRage.Game.MySimpleObjectDraw.DrawLine(from, to, material, ref auxcolor, 0.15f * (currentHeat / attractorWeaponInfo.maxHeat + 0.2f));
                    VRage.Game.MySimpleObjectDraw.DrawLine(from, to, material, ref maincolor, 0.5f * (currentHeat / attractorWeaponInfo.maxHeat + 0.2f));
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
            int chargesInInventory = (int)m_inventory.GetItemAmount(chargeDefinitionIds[damageUpgrades]);
            if (chargesInInventory < attractorWeaponInfo.keepAtCharge) {

				if (resourceSink.RequiredInputByType(electricityDefinition) != (attractorWeaponInfo.powerUsage/efficiencyUpgrades)) {
					
					resourceSink.SetRequiredInputByType (electricityDefinition, (attractorWeaponInfo.powerUsage/efficiencyUpgrades));

					setPowerConsumption = (attractorWeaponInfo.powerUsage/efficiencyUpgrades);
					powerConsumption = (attractorWeaponInfo.powerUsage/efficiencyUpgrades);

				} else {

					if (!functionalBlock.Enabled) {
						
						powerConsumption = 0.0001f;
					}
				}

				if (resourceSink.CurrentInputByType (electricityDefinition) == (attractorWeaponInfo.powerUsage/efficiencyUpgrades)) {

					if (!overheated) {
						m_inventory.AddItems ((MyFixedPoint)(attractorWeaponInfo.keepAtCharge - chargesInInventory), chargeObjectBuilders [damageUpgrades]);
					}
				}

			} else if(chargesInInventory > attractorWeaponInfo.keepAtCharge) {
				
				m_inventory.RemoveItemsOfType ((MyFixedPoint)(chargesInInventory - attractorWeaponInfo.keepAtCharge), chargeObjectBuilders [damageUpgrades]);

			} else  {
				
				if (setPowerConsumption != 0.0001f) {

					resourceSink.SetRequiredInputByType (electricityDefinition, 0.0001f);

					setPowerConsumption = 0.0001f;
					powerConsumption = 0.0001f;
				}
			}

            terminalBlock.RefreshCustomInfo ();
		}

		public override void Close ()
		{

			if (m_inventory != null) {

				for (int i = 0; i < attractorWeaponInfo.classes; i++) { 
					m_inventory.RemoveItemsOfType (m_inventory.GetItemAmount (chargeDefinitionIds[i]), chargeObjectBuilders[i]);
				}
			}

			base.Close ();
		}

		public override void MarkForClose ()
		{
			if (m_inventory != null) {

				for (int i = 0; i < attractorWeaponInfo.classes; i++) { 
					m_inventory.RemoveItemsOfType (m_inventory.GetItemAmount (chargeDefinitionIds[i]), chargeObjectBuilders[i]);
				}
			}

			base.MarkForClose ();
		}

	}
}

