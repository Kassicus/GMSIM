using GMSimulator.Models;
using GMSimulator.Models.Enums;

namespace GMSimulator.Systems;

public static class OverallCalculator
{
    public static int Calculate(Position position, PlayerAttributes a)
    {
        float raw = position switch
        {
            Position.QB => CalculateQB(a),
            Position.HB => CalculateHB(a),
            Position.FB => CalculateFB(a),
            Position.WR => CalculateWR(a),
            Position.TE => CalculateTE(a),
            Position.LT or Position.RT => CalculateOT(a),
            Position.LG or Position.RG => CalculateOG(a),
            Position.C => CalculateC(a),
            Position.EDGE => CalculateEDGE(a),
            Position.DT => CalculateDT(a),
            Position.MLB => CalculateMLB(a),
            Position.OLB => CalculateOLB(a),
            Position.CB => CalculateCB(a),
            Position.FS => CalculateFS(a),
            Position.SS => CalculateSS(a),
            Position.K => CalculateK(a),
            Position.P => CalculateP(a),
            Position.LS => CalculateLS(a),
            _ => 50
        };

        return Math.Clamp((int)raw, 40, 99);
    }

    private static float CalculateQB(PlayerAttributes a) =>
        a.ThrowPower * 0.12f +
        a.ShortAccuracy * 0.14f +
        a.MediumAccuracy * 0.14f +
        a.DeepAccuracy * 0.10f +
        a.ThrowOnRun * 0.08f +
        a.PlayAction * 0.05f +
        a.Speed * 0.04f +
        a.Acceleration * 0.03f +
        a.Awareness * 0.12f +
        a.Clutch * 0.05f +
        a.Elusiveness * 0.03f +
        a.Carrying * 0.02f +
        a.Stamina * 0.03f +
        a.Toughness * 0.03f +
        a.InjuryResistance * 0.02f;

    private static float CalculateHB(PlayerAttributes a) =>
        a.Speed * 0.12f +
        a.Acceleration * 0.10f +
        a.Agility * 0.06f +
        a.Carrying * 0.10f +
        a.BallCarrierVision * 0.10f +
        a.BreakTackle * 0.08f +
        a.Trucking * 0.05f +
        a.Elusiveness * 0.07f +
        a.JukeMove * 0.04f +
        a.SpinMove * 0.03f +
        a.StiffArm * 0.03f +
        a.Catching * 0.04f +
        a.Stamina * 0.04f +
        a.Toughness * 0.04f +
        a.Awareness * 0.05f +
        a.InjuryResistance * 0.03f +
        a.Strength * 0.02f;

    private static float CalculateFB(PlayerAttributes a) =>
        a.RunBlock * 0.18f +
        a.ImpactBlock * 0.12f +
        a.LeadBlock * 0.12f +
        a.Strength * 0.10f +
        a.Carrying * 0.06f +
        a.Trucking * 0.06f +
        a.Catching * 0.06f +
        a.Speed * 0.05f +
        a.Acceleration * 0.05f +
        a.Awareness * 0.08f +
        a.Toughness * 0.06f +
        a.Stamina * 0.03f +
        a.InjuryResistance * 0.03f;

    private static float CalculateWR(PlayerAttributes a) =>
        a.Speed * 0.10f +
        a.Acceleration * 0.08f +
        a.Agility * 0.05f +
        a.Catching * 0.12f +
        a.CatchInTraffic * 0.08f +
        a.SpectacularCatch * 0.05f +
        a.RouteRunning * 0.12f +
        a.Release * 0.08f +
        a.Jumping * 0.04f +
        a.Awareness * 0.08f +
        a.Elusiveness * 0.04f +
        a.Carrying * 0.03f +
        a.Stamina * 0.03f +
        a.Toughness * 0.03f +
        a.InjuryResistance * 0.02f +
        a.Strength * 0.02f +
        a.Clutch * 0.03f;

    private static float CalculateTE(PlayerAttributes a) =>
        a.Catching * 0.10f +
        a.CatchInTraffic * 0.06f +
        a.RouteRunning * 0.08f +
        a.RunBlock * 0.12f +
        a.PassBlock * 0.06f +
        a.Speed * 0.06f +
        a.Acceleration * 0.05f +
        a.Strength * 0.08f +
        a.Awareness * 0.08f +
        a.Release * 0.05f +
        a.Jumping * 0.04f +
        a.SpectacularCatch * 0.03f +
        a.Carrying * 0.03f +
        a.Toughness * 0.05f +
        a.Stamina * 0.04f +
        a.InjuryResistance * 0.03f +
        a.Agility * 0.04f;

    private static float CalculateOT(PlayerAttributes a) =>
        a.PassBlock * 0.20f +
        a.RunBlock * 0.18f +
        a.Strength * 0.12f +
        a.Awareness * 0.10f +
        a.ImpactBlock * 0.08f +
        a.Agility * 0.06f +
        a.Acceleration * 0.05f +
        a.Speed * 0.03f +
        a.Toughness * 0.06f +
        a.Stamina * 0.05f +
        a.InjuryResistance * 0.04f +
        a.Consistency * 0.03f;

    private static float CalculateOG(PlayerAttributes a) =>
        a.RunBlock * 0.20f +
        a.PassBlock * 0.18f +
        a.Strength * 0.14f +
        a.ImpactBlock * 0.10f +
        a.Awareness * 0.10f +
        a.Agility * 0.04f +
        a.Acceleration * 0.04f +
        a.Toughness * 0.06f +
        a.Stamina * 0.05f +
        a.InjuryResistance * 0.04f +
        a.Consistency * 0.03f +
        a.Speed * 0.02f;

    private static float CalculateC(PlayerAttributes a) =>
        a.RunBlock * 0.18f +
        a.PassBlock * 0.18f +
        a.Strength * 0.12f +
        a.Awareness * 0.14f +
        a.ImpactBlock * 0.08f +
        a.Agility * 0.05f +
        a.Acceleration * 0.04f +
        a.Toughness * 0.06f +
        a.Stamina * 0.05f +
        a.InjuryResistance * 0.04f +
        a.Consistency * 0.04f +
        a.Speed * 0.02f;

    private static float CalculateEDGE(PlayerAttributes a) =>
        a.Speed * 0.08f +
        a.Acceleration * 0.07f +
        a.FinesseMoves * 0.12f +
        a.PowerMoves * 0.12f +
        a.BlockShedding * 0.10f +
        a.Tackle * 0.08f +
        a.Pursuit * 0.08f +
        a.PlayRecognition * 0.08f +
        a.Strength * 0.06f +
        a.HitPower * 0.05f +
        a.Awareness * 0.05f +
        a.Stamina * 0.04f +
        a.Toughness * 0.04f +
        a.InjuryResistance * 0.03f;

    private static float CalculateDT(PlayerAttributes a) =>
        a.Strength * 0.12f +
        a.PowerMoves * 0.12f +
        a.BlockShedding * 0.14f +
        a.Tackle * 0.10f +
        a.Pursuit * 0.06f +
        a.PlayRecognition * 0.10f +
        a.FinesseMoves * 0.08f +
        a.HitPower * 0.06f +
        a.Awareness * 0.06f +
        a.Acceleration * 0.04f +
        a.Toughness * 0.05f +
        a.Stamina * 0.04f +
        a.InjuryResistance * 0.03f;

    private static float CalculateMLB(PlayerAttributes a) =>
        a.Tackle * 0.12f +
        a.Pursuit * 0.10f +
        a.PlayRecognition * 0.12f +
        a.ZoneCoverage * 0.08f +
        a.BlockShedding * 0.08f +
        a.HitPower * 0.06f +
        a.Speed * 0.06f +
        a.Acceleration * 0.05f +
        a.Awareness * 0.08f +
        a.ManCoverage * 0.04f +
        a.Strength * 0.05f +
        a.Agility * 0.03f +
        a.Toughness * 0.05f +
        a.Stamina * 0.04f +
        a.InjuryResistance * 0.04f;

    private static float CalculateOLB(PlayerAttributes a) =>
        a.Tackle * 0.10f +
        a.Pursuit * 0.08f +
        a.PlayRecognition * 0.10f +
        a.Speed * 0.08f +
        a.Acceleration * 0.06f +
        a.ZoneCoverage * 0.08f +
        a.ManCoverage * 0.05f +
        a.BlockShedding * 0.08f +
        a.HitPower * 0.06f +
        a.Awareness * 0.08f +
        a.FinesseMoves * 0.05f +
        a.Strength * 0.04f +
        a.Toughness * 0.05f +
        a.Stamina * 0.04f +
        a.InjuryResistance * 0.03f +
        a.Agility * 0.02f;

    private static float CalculateCB(PlayerAttributes a) =>
        a.ManCoverage * 0.14f +
        a.ZoneCoverage * 0.10f +
        a.Speed * 0.12f +
        a.Acceleration * 0.08f +
        a.Agility * 0.06f +
        a.Press * 0.08f +
        a.PlayRecognition * 0.10f +
        a.Jumping * 0.04f +
        a.Tackle * 0.04f +
        a.Awareness * 0.08f +
        a.Catching * 0.03f +
        a.Pursuit * 0.03f +
        a.Toughness * 0.03f +
        a.Stamina * 0.04f +
        a.InjuryResistance * 0.03f;

    private static float CalculateFS(PlayerAttributes a) =>
        a.ZoneCoverage * 0.14f +
        a.PlayRecognition * 0.12f +
        a.Speed * 0.10f +
        a.Acceleration * 0.06f +
        a.Pursuit * 0.08f +
        a.Tackle * 0.06f +
        a.ManCoverage * 0.08f +
        a.Awareness * 0.10f +
        a.Catching * 0.04f +
        a.Jumping * 0.04f +
        a.HitPower * 0.04f +
        a.Agility * 0.04f +
        a.Toughness * 0.04f +
        a.Stamina * 0.03f +
        a.InjuryResistance * 0.03f;

    private static float CalculateSS(PlayerAttributes a) =>
        a.Tackle * 0.10f +
        a.HitPower * 0.08f +
        a.ZoneCoverage * 0.10f +
        a.PlayRecognition * 0.10f +
        a.Speed * 0.08f +
        a.Acceleration * 0.06f +
        a.Pursuit * 0.08f +
        a.ManCoverage * 0.06f +
        a.Awareness * 0.08f +
        a.Strength * 0.05f +
        a.BlockShedding * 0.04f +
        a.Catching * 0.03f +
        a.Jumping * 0.03f +
        a.Toughness * 0.05f +
        a.Stamina * 0.03f +
        a.InjuryResistance * 0.03f;

    private static float CalculateK(PlayerAttributes a) =>
        a.KickPower * 0.30f +
        a.KickAccuracy * 0.35f +
        a.Awareness * 0.10f +
        a.Clutch * 0.10f +
        a.Consistency * 0.10f +
        a.Stamina * 0.03f +
        a.InjuryResistance * 0.02f;

    private static float CalculateP(PlayerAttributes a) =>
        a.KickPower * 0.35f +
        a.KickAccuracy * 0.30f +
        a.Awareness * 0.10f +
        a.Consistency * 0.10f +
        a.Stamina * 0.05f +
        a.Clutch * 0.05f +
        a.Speed * 0.03f +
        a.InjuryResistance * 0.02f;

    private static float CalculateLS(PlayerAttributes a) =>
        a.Awareness * 0.25f +
        a.Consistency * 0.30f +
        a.Strength * 0.10f +
        a.Stamina * 0.10f +
        a.Toughness * 0.10f +
        a.InjuryResistance * 0.10f +
        a.Clutch * 0.05f;
}
