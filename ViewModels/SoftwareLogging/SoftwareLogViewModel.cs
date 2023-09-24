using System.Collections.ObjectModel;

namespace SWTORCombatParser.ViewModels.SoftwareLogging
{
    public class SoftwareLogViewModel
    {
        public ObservableCollection<SoftwareLogInstance> SoftwareLogs { get; set; } = new ObservableCollection<SoftwareLogInstance>();

        internal void AddNewLog(SoftwareLogInstance log)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                SoftwareLogs.Insert(0, log);
            });

        }
    }
}
