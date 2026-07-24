using System;

namespace TractorBeam
{
	public static class TractorBeamManager
	{
		// 		AttractorWeaponInfo: PowerUsage, Damage, AmmoName, Classes, MaxHeat, HeatPerTick, HeatDissipationPerTick, HeatDissipationDelay, KeepAtCharge
		public static readonly AttractorWeaponInfo LargeBlockAttractorTurret = new AttractorWeaponInfo(12f, 10f, "LargeAttractorEnergyCell", 1, 1000f, 30f, 20f, 100, 10);
	}
}

