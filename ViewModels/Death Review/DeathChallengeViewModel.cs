﻿using SWTORCombatParser.Model.Challenge;
using SWTORCombatParser.ViewModels.Challenges;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.ViewModels.Death_Review
{
    public class DeathChallengeViewModel : INotifyPropertyChanged
    {
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        public event PropertyChangedEventHandler PropertyChanged;
        private ChallengeUpdater _challengeUpdater;

        public ObservableCollection<ChallengeInstanceViewModel> ActiveChallengeInstances { get; set; } = new ObservableCollection<ChallengeInstanceViewModel>();
        public DeathChallengeViewModel() {
            _challengeUpdater = new ChallengeUpdater();
            _challengeUpdater.SetCollection(ActiveChallengeInstances);
        }

    }
}
