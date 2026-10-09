using System;
namespace CrazyRubbish.Core
{
    public static class DesignRules
    {
        public static double InputRate(double seconds) => seconds < 600 ? 8 : seconds < 1800 ? 12 : 12 * Math.Pow(1.25, 1 + Math.Floor((seconds - 1800) / 1800));
        public static double InputMeanInterval(double seconds)
        {
            if (seconds < 600) return 60;
            if (seconds < 1800) return 54;
            if (seconds < 3600) return 48;
            if (seconds < 5400) return 42;
            return Math.Max(10, 42 * Math.Pow(0.9, 1 + Math.Floor((seconds - 5400) / 1800)));
        }
        public static double HappinessEfficiency(double happiness) => Math.Max(0.7, Math.Min(1.05, 0.7 + 0.3 * happiness / 70));
        public static double HumanEfficiency(double tool, double bonus, double happiness, double debt, double food, double water, double area, double height) =>
            Math.Max(0.25, Math.Min(1.6, tool * (1 + bonus) * HappinessEfficiency(happiness) * Math.Max(0.6, 1 - 0.1 * debt) * food * water * area * height));
        public static double HappinessLoss(double missingSeconds, double graceSeconds, double growthSeconds, double maximum) =>
            Math.Min(maximum, Math.Max(0, missingSeconds - graceSeconds) * maximum / growthSeconds);
        public static long RiotPayment(long openingDebtCents) => (openingDebtCents + 2) / 3;
        public const double ElectricWoodHumanSeconds = 10, ElectricWoodMachineSeconds = 15;
    }
}
