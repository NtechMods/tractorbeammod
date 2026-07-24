using System;

namespace TractorBeam
{
	public struct AttractorWeaponInfo
	{
		public float PowerUsage { get; }
		public float Damage { get; }
		public string AmmoName { get; }
		public int Classes { get; }
		public float MaxHeat { get; }
		public float HeatPerTick { get; }
		public float HeatDissipationPerTick { get; }
		public int HeatDissipationDelay { get; }
		public int KeepAtCharge { get; }

		public AttractorWeaponInfo(float powerUsage, float damage, string ammoName, int classes, float maxHeat, float heatPerTick, float heatDissipationPerTick, int heatDissipationDelay, int keepAtCharge)
		{
			PowerUsage = powerUsage;
			Damage = damage;
			AmmoName = ammoName;
			Classes = classes;
			MaxHeat = maxHeat;
			HeatPerTick = heatPerTick;
			HeatDissipationPerTick = heatDissipationPerTick;
			HeatDissipationDelay = heatDissipationDelay;
			KeepAtCharge = keepAtCharge;
		}
	}
}

