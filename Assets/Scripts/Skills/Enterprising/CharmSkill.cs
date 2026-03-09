namespace SunodGame.Demo
{
    public partial class DemoGameplayManager
    {
        private void UseCharm()
        {
            _catFrozenUntil = UnityEngine.Time.time + 4f;
            ShowToast("Charm used: cat is frozen.");
        }
    }
}
