using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using POEApi.Model;
using Procurement.ViewModel;
using Procurement.ViewModel.Filters;
using System.Diagnostics;
using POEApi.Infrastructure;

namespace Procurement.Controls
{
    public partial class StashControl : UserControl
    {
        public int TabNumber { get; set; }
        private bool initialized = false;

        private Dictionary<Tuple<int, int>, Item> stashByLocation;
        private Dictionary<Tuple<int, int>, Border> borderByLocation;
        public List<Item> Stash { get; set; }
        public int FilterResults { get; private set; }

        public IEnumerable<IFilter> Filter
        {
            get { return (IEnumerable<IFilter>) GetValue(FilterProperty); }
            set { SetValue(FilterProperty, value); }
        }

        public void ForceUpdate()
        {
            if (initialized == false && Stash == null)
                refresh();

            FilterResults = Filter.Count() == 0 ? -1 : 0;

            foreach (var item in Stash)
            {
                var index = Tuple.Create<int, int>(item.X, item.Y);

                // Currency tab does not have the standard 12x12 grid
                // so we have to check each column exists before attempting to access it
                if (borderByLocation.ContainsKey(index))
                {
                    updateResult(borderByLocation[index], search(item));
                }
            }

            this.UpdateLayout();
        }

        private void updateResult(Border border, bool isResult)
        {
            if (isResult)
            {
                FilterResults++;
                border.BorderBrush = Brushes.Yellow;
                border.Background = Brushes.Black;
                return;
            }

            border.BorderBrush = Brushes.Transparent;
            border.Background = Brushes.Transparent;
        }

        public void RefreshTab(string accountName)
        {
            ApplicationState.Stash[ApplicationState.CurrentLeague].RefreshTab(ApplicationState.Model,
                ApplicationState.CurrentLeague, TabNumber, accountName);
            refresh();
        }

        public static readonly DependencyProperty FilterProperty =
            DependencyProperty.Register("Filter", typeof(IEnumerable<IFilter>), typeof(StashControl), null);

        public StashControl()
        {
            InitializeComponent();
            this.Loaded += new RoutedEventHandler(StashControl_Loaded);
            ApplicationState.LeagueChanged +=
                new System.ComponentModel.PropertyChangedEventHandler(ApplicationState_LeagueChanged);
            stashByLocation = new Dictionary<Tuple<int, int>, Item>();
        }

        void ApplicationState_LeagueChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            initialized = false;
        }

        void StashControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (initialized)
                return;

            refresh();
        }

        private void refresh()
        {
            this.Stash = ApplicationState.Stash[ApplicationState.CurrentLeague].GetItemsByTab(TabNumber);
            TabType tabType = GetTabType();

            updateStashByLocation();
            render(tabType);
        }

        private TabType GetTabType()
        {
            try
            {
                return ApplicationState.Stash[ApplicationState.CurrentLeague].Tabs[TabNumber].Type;
            }
            catch (Exception ex)
            {
                Logger.Log("Error in StashControl.GetTabType: " + ex);
                return TabType.Normal;
            }
        }

        private void updateStashByLocation()
        {

            stashByLocation.Clear();


            foreach (var item in this.Stash)
            {
                var key = Tuple.Create<int, int>(item.X, item.Y);

                if (stashByLocation.ContainsKey(key))
                    continue;

                stashByLocation.Add(key, item);
            }
        }

        private const int NORMAL_SPACING = 12;
        private const int QUAD_SPACING = 24;

        private void render(TabType tabType)
        {
            int columns = NORMAL_SPACING, rows = NORMAL_SPACING;

            if (tabType == TabType.Quad)
            {
                columns = QUAD_SPACING;
                rows = QUAD_SPACING;
            }

            grid.ColumnDefinitions.Clear();
            grid.RowDefinitions.Clear();
            grid.Children.Clear();

            borderByLocation = new Dictionary<Tuple<int, int>, Border>();

            for (int i = 0; i < columns; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition());
                for (int j = 0; j < rows; j++)
                {
                    if (i == 0)
                        grid.RowDefinitions.Add(new RowDefinition());

                    Grid childGrid = new Grid();
                    childGrid.Margin = new Thickness(1);

                    Tuple<int, int> currentKey = new Tuple<int, int>(i, j);

                    if (!stashByLocation.ContainsKey(currentKey))
                        continue;

                    Item gearAtLocation = stashByLocation[currentKey];

                    setBackround(childGrid, gearAtLocation);
                    //if (search(gearAtLocation))
                    Border border = getBorder();
                    borderByLocation[currentKey] = border;
                    childGrid.Children.Add(border);

                    childGrid.Children.Add(getImage(gearAtLocation));

                    Grid.SetColumn(childGrid, i);
                    Grid.SetRow(childGrid, j);
                    if (gearAtLocation.H > 1)
                        Grid.SetRowSpan(childGrid, gearAtLocation.H);

                    if (gearAtLocation.W > 1)
                        Grid.SetColumnSpan(childGrid, gearAtLocation.W);

                    grid.Children.Add(childGrid);
                }
            }

            initialized = true;
        }

        private UIElement getImage(Item item)
        {
            return new ItemDisplay() { DataContext = new ItemDisplayViewModel(item) };
        }

        void popup_LostMouseCapture(object sender, MouseEventArgs e)
        {
            (sender as Popup).IsOpen = false;
        }


        private Border getBorder()
        {
            Border b = new Border();
            b.BorderBrush = Brushes.Transparent;
            b.BorderThickness = new Thickness(2);
            return b;
        }

        public enum Stat
        {
            Life,
            EnergyShield,
            EnergyShieldMult,
            Strength,
            Dexterity,
            Intelligence,
            TotalStats,
            Resistance,
            Accuracy,
            MovementSpeed,
            AttackSpeed,
            SpellDamage,
            SpellCritChance,
            CritChance,
            CritMult,
            GemLevel,
            FireDamageAttack,
            ColdDamageAttack,
            LightningDamageAttack,
            FireDamageSpell,
            ColdDamageSpell,
            LightningDamageSpell,
            PhysicalDamageMult,
            PhysicalDamageAdd,
            CastSpeed,
            Armor,
            WeaponElemDamage,
            FlaskChargesUsed,
            FlaskChargesGained,
            FlaskEffectDuration,
            ManaRegen,
            IncreasedRarity,
        }

        class ParseData
        {
            public Stat stat;
            public Regex regex;
            public int parses;
        }

        static ParseData[] parseData = new ParseData[] {
            new ParseData{ stat = Stat.Life, regex = new Regex(@"\+([0-9]+) to maximum Life$") },
            new ParseData{ stat = Stat.EnergyShield, regex = new Regex(@"\+([0-9]+) to maximum Energy Shield$") },
            new ParseData{ stat = Stat.EnergyShieldMult, regex = new Regex(@"([0-9]+)% increased Energy Shield$") },
            new ParseData{ stat = Stat.Strength, regex = new Regex(@"\+([0-9]+) to Strength$") },
            new ParseData{ stat = Stat.Strength, regex = new Regex(@"\+([0-9]+) to All Attributes$") },
            new ParseData{ stat = Stat.Dexterity, regex = new Regex(@"\+([0-9]+) to Dexterity$") },
            new ParseData{ stat = Stat.Dexterity, regex = new Regex(@"\+([0-9]+) to All Attributes$") },
            new ParseData{ stat = Stat.Intelligence, regex = new Regex(@"\+([0-9]+) to Intelligence$") },
            new ParseData{ stat = Stat.Intelligence, regex = new Regex(@"\+([0-9]+) to All Attributes$") },
            new ParseData{ stat = Stat.Resistance, regex = new Regex(@"\+([0-9]+)% to Fire Resistance$") },
            new ParseData{ stat = Stat.Resistance, regex = new Regex(@"\+([0-9]+)% to Cold Resistance$") },
            new ParseData{ stat = Stat.Resistance, regex = new Regex(@"\+([0-9]+)% to Lightning Resistance$") },
            new ParseData{ stat = Stat.Resistance, regex = new Regex(@"\+([0-9]+)% to Chaos Resistance$") },
            new ParseData{ stat = Stat.Resistance, regex = new Regex(@"\+([0-9]+)% to all Elemental Resistances$") },
            new ParseData{ stat = Stat.Resistance, regex = new Regex(@"\+([0-9]+)% to all Elemental Resistances$") },
            new ParseData{ stat = Stat.Resistance, regex = new Regex(@"\+([0-9]+)% to all Elemental Resistances$") },
            new ParseData{ stat = Stat.Accuracy, regex = new Regex(@"\+([0-9]+) to Accuracy Rating$") },
            new ParseData{ stat = Stat.MovementSpeed, regex = new Regex(@"([0-9]+)% increased Movement Speed$") },
            new ParseData{ stat = Stat.AttackSpeed, regex = new Regex(@"([0-9]+)% increased Attack Speed$") },
            new ParseData{ stat = Stat.SpellDamage, regex = new Regex(@"([0-9]+)% increased Spell Damage$") },
            new ParseData{ stat = Stat.SpellCritChance, regex = new Regex(@"([0-9]+)% increased Critical Strike Chance for Spells$") },
            new ParseData{ stat = Stat.SpellCritChance, regex = new Regex(@"([0-9]+)% increased Global Critical Strike Chance$") },
            new ParseData{ stat = Stat.CritChance, regex = new Regex(@"([0-9]+)% increased Global Critical Strike Chance$") },
            new ParseData{ stat = Stat.CritMult, regex = new Regex(@"([0-9]+)% to Global Critical Strike Multiplier$") },
            new ParseData{ stat = Stat.GemLevel, regex = new Regex(@"\+([0-9]+) to Level of Socketed .*Gems$") },
            new ParseData{ stat = Stat.FireDamageAttack, regex = new Regex(@"Adds [0-9]+ to ([0-9]+) Fire Damage$") },
            new ParseData{ stat = Stat.ColdDamageAttack, regex = new Regex(@"Adds [0-9]+ to ([0-9]+) Cold Damage$") },
            new ParseData{ stat = Stat.LightningDamageAttack, regex = new Regex(@"Adds [0-9]+ to ([0-9]+) Lightning Damage$") },
            new ParseData{ stat = Stat.FireDamageSpell, regex = new Regex(@"Adds [0-9]+ to ([0-9]+) Fire Damage to Spells$") },
            new ParseData{ stat = Stat.ColdDamageSpell, regex = new Regex(@"Adds [0-9]+ to ([0-9]+) Cold Damage to Spells$") },
            new ParseData{ stat = Stat.LightningDamageSpell, regex = new Regex(@"Adds [0-9]+ to ([0-9]+) Lightning Damage to Spells$") },
            new ParseData{ stat = Stat.PhysicalDamageMult, regex = new Regex(@"([0-9]+)% increased Physical Damage$") },
            new ParseData{ stat = Stat.PhysicalDamageAdd, regex = new Regex(@"Adds [0-9]+ to ([0-9]+) Physical Damage$") },
            new ParseData{ stat = Stat.CastSpeed, regex = new Regex(@"([0-9]+)% increased Cast Speed$") },
            new ParseData{ stat = Stat.Armor, regex = new Regex(@"\+([0-9]+) to Armour$") },
            new ParseData{ stat = Stat.WeaponElemDamage, regex = new Regex(@"([0-9]+)% increased Elemental Damage with Attack Skills$") },
            new ParseData{ stat = Stat.FlaskChargesUsed, regex = new Regex(@"([0-9]+)% reduced Flask Charges used$") },
            new ParseData{ stat = Stat.FlaskChargesGained, regex = new Regex(@"([0-9]+)% increased Flask Charges gained$") },
            new ParseData{ stat = Stat.FlaskEffectDuration, regex = new Regex(@"([0-9]+)% increased Flask effect duration$") },
            new ParseData{ stat = Stat.ManaRegen, regex = new Regex(@"([0-9]+)% increased Mana Regeneration Rate$") },
            new ParseData{ stat = Stat.IncreasedRarity, regex = new Regex(@"([0-9]+)% increased Rarity of Items found$") },
        };

        private void setBackround(Grid childGrid, Item item)
        {
            if (item is Gear && (item as Gear).Rarity != Rarity.Normal && (item as Gear).Explicitmods == null)
            {
                childGrid.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#000000"));
            }
            else if (item is Gear && (item as Gear).Rarity == Rarity.Unique)
            {
                childGrid.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ffffff"));
            }
            else
            {
                childGrid.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#21007F"));

                var gear = item as Gear;
                if (gear != null && gear.GearType != GearType.Jewel && gear.Rarity != Rarity.Unique)
                {
                    Dictionary<Stat, float> values = new Dictionary<Stat, float>();
                    
                    List<String> statlines = new List<string>();

                    if (gear.Implicitmods != null)
                    {
                        statlines.AddRange(gear.Implicitmods);
                    }

                    if (gear.Explicitmods != null)
                    {
                        statlines.AddRange(gear.Explicitmods);
                    }
                    
                    foreach (var statline in statlines)
                    {
                        foreach (var pd in parseData)
                        {
                            var result = pd.regex.Match(statline);
                            if (result.Success)
                            {
                                if (!values.ContainsKey(pd.stat))
                                {
                                    values[pd.stat] = 0;
                                }

                                values[pd.stat] += float.Parse(result.Groups[1].Value);

                                pd.parses++;
                            }
                        }
                    }

                    if (gear.Properties != null)
                    {
                        var prop = gear.Properties.Where(p => p.Name == "Energy Shield").FirstOrDefault();
                        if (prop != null)
                        {
                            values[Stat.EnergyShield] = float.Parse(prop.Values[0].Item1);
                        }
                    }

                    if (values.TryGetValue(Stat.Strength) > 0)
                    {
                        if (!values.ContainsKey(Stat.Life))
                        {
                            values[Stat.Life] = 0;
                        }
                        values[Stat.Life] += values.TryGetValue(Stat.Strength) / 2;
                    }

                    values[Stat.TotalStats] = values.TryGetValue(Stat.Strength) + values.TryGetValue(Stat.Intelligence) + values.TryGetValue(Stat.Dexterity);

                    float validations = 0;
                    bool seen = false;

                    childGrid.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#88001D"));
                    if (gear.GearType == GearType.Chest)
                    {
                        seen = true;
                        validations += values.CalculateValidations(Stat.Life, 75);
                        validations += values.CalculateValidations(Stat.EnergyShield, 575);
                        validations += values.CalculateValidations(Stat.EnergyShield, 350, Stat.Life, 65);
                        validations += values.CalculateValidations(Stat.Strength, 40);
                        validations += values.CalculateValidations(Stat.Intelligence, 40);
                        validations += values.CalculateValidations(Stat.Resistance, 80);
                    }

                    if (gear.GearType == GearType.Helmet)
                    {
                        seen = true;
                        validations += values.CalculateValidations(Stat.Life, 65);
                        validations += values.CalculateValidations(Stat.EnergyShield, 300);
                        validations += values.CalculateValidations(Stat.EnergyShield, 200, Stat.Life, 55);
                        validations += values.CalculateValidations(Stat.Accuracy, 300);
                        validations += values.CalculateValidations(Stat.Intelligence, 40);
                        validations += values.CalculateValidations(Stat.Resistance, 80);
                    }

                    if (gear.GearType == GearType.Boots)
                    {
                        seen = true;
                        validations += values.CalculateValidations(Stat.MovementSpeed, 20);
                        validations += values.CalculateValidations(Stat.Life, 65);
                        validations += values.CalculateValidations(Stat.EnergyShield, 130);
                        validations += values.CalculateValidations(Stat.EnergyShield, 90, Stat.Life, 55);
                        validations += values.CalculateValidations(Stat.Strength, 40);
                        validations += values.CalculateValidations(Stat.Intelligence, 40);
                        validations += values.CalculateValidations(Stat.Resistance, 70);
                    }

                    if (gear.GearType == GearType.Gloves)
                    {
                        seen = true;
                        validations += values.CalculateValidations(Stat.Life, 65);
                        validations += values.CalculateValidations(Stat.EnergyShield, 150);
                        validations += values.CalculateValidations(Stat.EnergyShield, 100, Stat.Life, 55);
                        validations += values.CalculateValidations(Stat.Resistance, 80);
                        validations += values.CalculateValidations(Stat.Accuracy, 300);
                        validations += values.CalculateValidations(Stat.AttackSpeed, 10);
                        validations += values.CalculateValidations(Stat.Dexterity, 40);
                    }

                    if (gear.GearType == GearType.Shield)
                    {
                        seen = true;
                        validations += values.CalculateValidations(Stat.Life, 80);
                        validations += values.CalculateValidations(Stat.EnergyShield, 350);
                        validations += values.CalculateValidations(Stat.EnergyShield, 280, Stat.Life, 70);
                        validations += values.CalculateValidations(Stat.Resistance, 100);
                        validations += values.CalculateValidations(Stat.Strength, 35);
                        validations += values.CalculateValidations(Stat.Intelligence, 35);
                        validations += values.CalculateValidations(Stat.SpellDamage, 55);
                        validations += values.CalculateValidations(Stat.SpellCritChance, 80);
                    }

                    if (gear.GearType == GearType.Sword || gear.GearType == GearType.Axe || gear.GearType == GearType.Mace || gear.GearType == GearType.Bow)
                    {
                        seen = true;
                        validations += values.CalculateValidations(Stat.PhysicalDamageMult, 170);
                        validations += values.CalculateValidations(Stat.PhysicalDamageAdd, 33);
                        validations += values.CalculateValidations(Stat.AttackSpeed, 20);
                        if (gear.GearType == GearType.Bow)
                        {
                            validations += values.CalculateValidations(Stat.CritChance, 30);
                            validations += values.CalculateValidations(Stat.CritMult, 30);
                        }
                        validations += values.CalculateValidations(Stat.GemLevel, 2);

                        validations += values.CalculateValidations(Stat.FireDamageAttack, 70);
                        validations += values.CalculateValidations(Stat.ColdDamageAttack, 70);
                        validations += values.CalculateValidations(Stat.LightningDamageAttack, 120);
                        validations += values.CalculateValidations(Stat.AttackSpeed, 20);
                    }

                    if (gear.GearType == GearType.Dagger || gear.GearType == GearType.Wand || gear.GearType == GearType.Sceptre || gear.GearType == GearType.Claw)
                    {
                        seen = true;
                        validations += values.CalculateValidations(Stat.SpellDamage, 90);
                        validations += values.CalculateValidations(Stat.SpellCritChance, 130);
                        validations += values.CalculateValidations(Stat.FireDamageSpell, 50);
                        validations += values.CalculateValidations(Stat.ColdDamageSpell, 50);
                        validations += values.CalculateValidations(Stat.LightningDamageSpell, 90);
                        validations += values.CalculateValidations(Stat.CritMult, 30);

                        validations += values.CalculateValidations(Stat.PhysicalDamageMult, 170);
                        validations += values.CalculateValidations(Stat.PhysicalDamageAdd, 33);
                        if (gear.GearType == GearType.Dagger) validations += values.CalculateValidations(Stat.AttackSpeed, 20);
                        if (gear.GearType == GearType.Wand) validations += values.CalculateValidations(Stat.AttackSpeed, 10);
                        validations += values.CalculateValidations(Stat.CritChance, 30);
                        validations += values.CalculateValidations(Stat.CritMult, 30);
                        validations += values.CalculateValidations(Stat.FireDamageAttack, 70);
                        validations += values.CalculateValidations(Stat.ColdDamageAttack, 70);
                        validations += values.CalculateValidations(Stat.LightningDamageAttack, 120);
                    }

                    if (gear.GearType == GearType.Staff)
                    {
                        seen = true;
                        validations += values.CalculateValidations(Stat.GemLevel, 2);
                        validations += values.CalculateValidations(Stat.FireDamageSpell, 70);
                        validations += values.CalculateValidations(Stat.ColdDamageSpell, 70);
                        validations += values.CalculateValidations(Stat.LightningDamageSpell, 150);
                        validations += values.CalculateValidations(Stat.SpellDamage, 160);
                    }

                    if (gear.GearType == GearType.Belt)
                    {
                        seen = true;
                        validations += values.CalculateValidations(Stat.Life, 70);
                        validations += values.CalculateValidations(Stat.Strength, 35);
                        validations += values.CalculateValidations(Stat.Armor, 280);
                        validations += values.CalculateValidations(Stat.EnergyShield, 45);
                        validations += values.CalculateValidations(Stat.Resistance, 70);
                        validations += values.CalculateValidations(Stat.WeaponElemDamage, 30);
                        validations += values.CalculateValidations(Stat.FlaskChargesGained, 10);
                        validations += values.CalculateValidations(Stat.FlaskChargesUsed, 10);
                        validations += values.CalculateValidations(Stat.FlaskEffectDuration, 10);
                    }

                    if (gear.GearType == GearType.Ring)
                    {
                        seen = true;
                        validations += values.CalculateValidations(Stat.Life, 55);
                        validations += values.CalculateValidations(Stat.Strength, 50);
                        validations += values.CalculateValidations(Stat.PhysicalDamageAdd, 11);
                        validations += values.CalculateValidations(Stat.WeaponElemDamage, 30);
                        validations += values.CalculateValidations(Stat.IncreasedRarity, 40);
                        validations += values.CalculateValidations(Stat.Resistance, 80);
                        validations += values.CalculateValidations(Stat.ManaRegen, 50);
                        validations += values.CalculateValidations(Stat.Accuracy, 250);
                        validations += values.CalculateValidations(Stat.TotalStats, 75);
                    }

                    if (gear.GearType == GearType.Amulet)
                    {
                        seen = true;
                        validations += values.CalculateValidations(Stat.Life, 55);
                        validations += values.CalculateValidations(Stat.PhysicalDamageAdd, 11);
                        validations += values.CalculateValidations(Stat.WeaponElemDamage, 30);
                        validations += values.CalculateValidations(Stat.IncreasedRarity, 40);
                        validations += values.CalculateValidations(Stat.Resistance, 90);
                        validations += values.CalculateValidations(Stat.ManaRegen, 65);
                        validations += values.CalculateValidations(Stat.Accuracy, 250);
                        validations += values.CalculateValidations(Stat.TotalStats, 70);
                        validations += values.CalculateValidations(Stat.CritMult, 30);
                        validations += values.CalculateValidations(Stat.CritChance, 30);
                        validations += values.CalculateValidations(Stat.SpellDamage, 30);
                        validations += values.CalculateValidations(Stat.EnergyShieldMult, 15);
                    }

                    if (gear.GearType == GearType.Quiver)
                    {
                        seen = true;
                        validations += values.CalculateValidations(Stat.Life, 75);
                        validations += values.CalculateValidations(Stat.WeaponElemDamage, 30);
                        validations += values.CalculateValidations(Stat.CritMult, 30);
                        validations += values.CalculateValidations(Stat.CritChance, 30);
                        validations += values.CalculateValidations(Stat.Resistance, 70);
                    }
                    
                    if (!seen)
                    {
                        childGrid.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ffffff"));
                    }
                    else if (validations >= 3)
                    {
                        childGrid.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00ff1d"));
                    }
                    else if (validations >= 2)
                    {
                        childGrid.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#008080"));
                    }
                    else if (validations >= 1)
                    {
                        childGrid.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#21007F"));
                    }

                    /*Logger.Log("===");
                    foreach (var pd in parseData)
                    {
                        Logger.Log(string.Format("{0}: {1}", pd.parses, pd.stat));
                    }*/
                }
            }

            childGrid.Background.Opacity = 0.3;
        }

        private bool search(Item item)
        {
            if (Filter.Count() == 0)
                return false;

            return Filter.All(filter => filter.Applicable(item));
        }
    }

    public static class utilliy
    {
        public static V TryGetValue<K, V>(this Dictionary<K, V> dict, K key)
        {
            V result = default(V);
            dict.TryGetValue(key, out result);
            return result;
        }

        const float adjustment = 1.2f;

        public static float CalculateValidations(this Dictionary<StashControl.Stat, float> dict, StashControl.Stat key1, float value1)
        {
            value1 /= adjustment;

            if( dict.TryGetValue(key1) < value1 )
                return 0;
            
            return dict.TryGetValue(key1) / value1;
        }

        public static float CalculateValidations(this Dictionary<StashControl.Stat, float> dict, StashControl.Stat key1, float value1, StashControl.Stat key2, float value2)
        {
            value1 /= adjustment;
            value2 /= adjustment;

            if( dict.TryGetValue(key1) < value1 || dict.TryGetValue(key2) < value2 )
                return 0;
            
            return (dict.TryGetValue(key1) / value1 + dict.TryGetValue(key2) / value2) / 2;
        }
    }
}
