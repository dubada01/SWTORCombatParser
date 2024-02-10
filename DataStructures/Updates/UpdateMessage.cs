using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.DataStructures.Updates
{
    public class UpdateMessage
    {
        public Guid MessageId { get; set; }
        public DateTime CreationTime { get; set; }
        public string ValidForBuild { get; set; }
        public double DurationHrs { get; set; }
        public string UpdateMessageHeader { get; set; }
        public string UpdateMessageBody { get; set;}
        public bool IsSoftwareUpdateMessage { get; set; }
    }
}
