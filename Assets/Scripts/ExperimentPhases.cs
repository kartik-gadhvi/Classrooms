public enum ExperimentPhase
{
    Introduction,
    WalkToApparatus,
    ApparatusTour,
    FirstSpray,
    GravityAndRadius,
    RadiusSliderIntro,
    RadiusTasks,
    RadiusWrapUp,
    ElectricField,
    MeasurementSpray,
    SelectDroplet,
    HoverDroplet,
    MeasurementLoop,
    Histogram,
    Notebook,
    Closing
}

public static class ExperimentPhases
{
    public static ExperimentPhase FromStep(int step)
    {
        if (step <= 3) return ExperimentPhase.Introduction;
        if (step == 4) return ExperimentPhase.WalkToApparatus;
        if (step <= 16) return ExperimentPhase.ApparatusTour;
        if (step == 17) return ExperimentPhase.FirstSpray;
        if (step <= 22) return ExperimentPhase.GravityAndRadius;
        if (step <= 24) return ExperimentPhase.RadiusSliderIntro;
        if (step <= 27) return ExperimentPhase.RadiusTasks;
        if (step == 28) return ExperimentPhase.RadiusWrapUp;
        if (step <= 32) return ExperimentPhase.ElectricField;
        if (step == 33) return ExperimentPhase.MeasurementSpray;
        if (step == 34) return ExperimentPhase.SelectDroplet;
        if (step == 35) return ExperimentPhase.HoverDroplet;
        if (step <= 40) return ExperimentPhase.MeasurementLoop;
        if (step <= 46) return ExperimentPhase.Histogram;
        if (step <= 57) return ExperimentPhase.Notebook;
        return ExperimentPhase.Closing;
    }

    public static string Describe(ExperimentPhase phase)
    {
        switch (phase)
        {
            case ExperimentPhase.Introduction: return "introduction";
            case ExperimentPhase.WalkToApparatus: return "walk to the apparatus";
            case ExperimentPhase.ApparatusTour: return "historical context and apparatus tour";
            case ExperimentPhase.FirstSpray: return "first spray task";
            case ExperimentPhase.GravityAndRadius: return "gravity and radius explanation";
            case ExperimentPhase.RadiusSliderIntro: return "radius slider intro";
            case ExperimentPhase.RadiusTasks: return "radius tasks";
            case ExperimentPhase.RadiusWrapUp: return "radius section wrap-up";
            case ExperimentPhase.ElectricField: return "electric field explanation";
            case ExperimentPhase.MeasurementSpray: return "measurement spray task";
            case ExperimentPhase.SelectDroplet: return "select a droplet";
            case ExperimentPhase.HoverDroplet: return "adjust voltage until the droplet hovers";
            case ExperimentPhase.MeasurementLoop: return "measurement formulas and repeat loop";
            case ExperimentPhase.Histogram: return "histogram and charge quantization";
            case ExperimentPhase.Notebook: return "notebook and data selection controversy";
            default: return "publication result and closing";
        }
    }
}
