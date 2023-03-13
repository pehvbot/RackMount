using System;
namespace RackMount
{
    public class RackMountSettings : GameParameters.CustomParameterNode
    {

        public RackMountSettings()
        {
        }

        public override string Title
        {
            get { return "RackMount Options"; }
        }

        public override string DisplaySection
        {
            get { return ""; }
        }

        public override string Section
        {
            get { return "RackMount"; }
        }

        public override int SectionOrder
        {
            get { return 1; }
        }

        public override GameParameters.GameMode GameMode
        {
            get { return GameParameters.GameMode.ANY; }
        }

        public override bool HasPresets
        {
            get { return true; }
        }

        [GameParameters.CustomParameterUI("Parts can always be rackmounted.", toolTip = "Enable for testing")]
        public bool canAlwaysRackmount = false;

        [GameParameters.CustomParameterUI("Requires an Engineer to rackmount in flight.", toolTip = "You will need an engineer to rackmount and unmount parts.")]
        public bool requiresEngineer = true;

        [GameParameters.CustomIntParameterUI("Maximum EVA distance",toolTip = "The maximum distance a kerbal can rackmount items from the host part.", minValue = 2, maxValue = 10)]
        public int evaDistance = 3;


        public override void SetDifficultyPreset(GameParameters.Preset preset)
        {
            switch (preset)
            {
                case GameParameters.Preset.Easy:
                    requiresEngineer = false;
                    break;

                case GameParameters.Preset.Normal:
                    requiresEngineer = true;
                    break;

                case GameParameters.Preset.Moderate:
                    requiresEngineer = true;
                    break;

                case GameParameters.Preset.Hard:
                    requiresEngineer = true;
                    break;
            }
        }
    }
}

