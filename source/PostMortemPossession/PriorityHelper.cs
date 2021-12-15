using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;

namespace PostMortemPossession
{
    class PriorityHelper
    {
        private Dictionary<UnitCategory, int> _priorities { get; set; }
        private List<UnitCategory> _higestPriorityOrder { get; set; }
        private bool _random { get; set; }

        public enum UnitCategory
        {
            Companions = 99,
            Infantry = FormationClass.Infantry,
            Ranged = FormationClass.Ranged,
            Cavalry = FormationClass.Cavalry,
            HorseArcher = FormationClass.HorseArcher,
            Skirmisher = FormationClass.Skirmisher,
            HeavyInfantry = FormationClass.HeavyInfantry,
            LightCavalry = FormationClass.LightCavalry,
            HeavyCavalry = FormationClass.HeavyCavalry
        };

        public PriorityHelper(int[] pPriorities, bool pRandom)
        {
            _priorities = new Dictionary<UnitCategory, int>
                {
                    { UnitCategory.Companions, pPriorities[0] },
                    { UnitCategory.Infantry, pPriorities[1] },
                    { UnitCategory.Ranged, pPriorities[2] },
                    { UnitCategory.Cavalry, pPriorities[3] },
                    { UnitCategory.HorseArcher, pPriorities[4] },
                    { UnitCategory.Skirmisher, pPriorities[5] },
                    { UnitCategory.HeavyInfantry, pPriorities[6] },
                    { UnitCategory.LightCavalry, pPriorities[7] },
                    { UnitCategory.HeavyCavalry, pPriorities[8] }
                };
            _random = pRandom;

            OrderPriority();
        }

        public List<UnitCategory> GetHigestPriorityOrder()
        {
            if (_random)
                OrderPriority();

            return _higestPriorityOrder;
        }

        private void OrderPriority()
        {
            var newHigestPriorityOrder = new List<UnitCategory>();
            var orderedPriorityGroups = _priorities.OrderByDescending(p => p.Value).GroupBy(p => p.Value);
            foreach (var group in orderedPriorityGroups)
            {
                if (_random && group.Count() > 1)
                {
                    var groupList = group.ToList<KeyValuePair<UnitCategory, int>>();
                    while (groupList.Any())
                    {
                        var randomEntry = groupList.GetRandomElement();
                        newHigestPriorityOrder.Add(randomEntry.Key);
                        groupList.Remove(randomEntry);
                    }
                }
                else
                    newHigestPriorityOrder.AddRange(group.Select(x => x.Key));
            }

            _higestPriorityOrder = newHigestPriorityOrder;
        }
    }
}
