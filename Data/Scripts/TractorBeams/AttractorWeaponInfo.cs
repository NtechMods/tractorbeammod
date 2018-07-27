using System;

namespace TractorBeam
{
	public class AttractorWeaponInfo
	{
		public float powerUsage;
		public float damage;

		public string ammoName;
		public int classes;

		public float maxHeat;
		public float heatPerTick;
		public float heatDissipationPerTick;
		public int heatDissipationDelay;
		
		public int keepAtCharge;

		public AttractorWeaponInfo (float powerUsage, float damage, string ammoName, int classes, float maxHeat, float heatPerTick, float heatDissipationPerTick, int heatDissipationDelay, int keepAtCharge) {

			this.powerUsage = powerUsage;
			this.damage = damage;
			this.ammoName = ammoName;
			this.classes = classes;
			this.maxHeat = maxHeat;
			this.heatPerTick = heatPerTick;
			this.heatDissipationPerTick = heatDissipationPerTick;
			this.heatDissipationDelay = heatDissipationDelay;
			this.keepAtCharge = keepAtCharge;
		}
	}
	

}

