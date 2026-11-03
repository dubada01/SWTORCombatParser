using System.Collections.Generic;

namespace SWTORCombatParser.DataStructures.EncounterInfo;

public static class LogIdFactory
{
    public static void AddIdsToEncounters(List<EncounterInfo> currentEncounters)
    {
        foreach (var encounter in currentEncounters)
        {
            switch (encounter.Name)
            {
                case "Eternity Vault":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Annihilation Droid XRR-3", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 1779997656219648L } },
                                { "Story 16", new List<long>() { 2017165750304768L } },
                                { "Veteran 8", new List<long>() { 2034573252755456L } },
                                { "Veteran 16", new List<long>() { 2034611907461120L } },
                            }
                        },
                        {
                            "Gharj", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 1783772932472832L } },
                                { "Story 16", new List<long>() { 2016946706972672L } },
                                { "Veteran 8", new List<long>() { 2034526008115200L } },
                                { "Veteran 16", new List<long>() { 2034534598049792L } },
                            }
                        },
                        {
                            "Soa", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 1783790112342016L } },
                                { "Story 16", new List<long>() { 2017170045272064L } },
                                { "Veteran 8", new List<long>() { 2289823159156736L } },
                                { "Veteran 16", new List<long>() { 2290085152161792L } },
                            }
                        }
                    };
                    break;
                case "Karagga's Palace":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Bonethrasher", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 2271801476382720L } },
                                { "Story 16", new List<long>() { 2624491305828352L } },
                                { "Veteran 8", new List<long>() { 2624474125959168L } },
                                { "Veteran 16", new List<long>() { 2624508485697536L } },
                            }
                        },
                        {
                            "Jarg & Sorno", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 2739437515571200L, 2739441810538496L } },
                                { "Story 16", new List<long>() { 2760500035190784L, 2760504330158080L } },
                                { "Veteran 8", new List<long>() { 2760482855321600L, 2760487150288896L } },
                                { "Veteran 16", new List<long>() { 2760517215059968L, 2760521510027264L } },
                            }
                        },
                        {
                            "Foreman Crusher", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 2739875602235392L } },
                                { "Story 16", new List<long>() { 2739888487137280L } },
                                { "Veteran 8", new List<long>() { 2760637474144256L } },
                                { "Veteran 16", new List<long>() { 2760693308719104L } },
                            }
                        },
                        {
                            "G4-B3 Heavy Fabricator", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 2747344550363136L } },
                                { "Story 16", new List<long>() { 2760371186171904L } },
                                { "Veteran 8", new List<long>() { 2748401112317952L } },
                                { "Veteran 16", new List<long>() { 2760375481139200L } },
                            }
                        }
                        ,
                        {
                            "Karagga the Unyielding", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 2740043105959936L } },
                                { "Story 16", new List<long>() { 2761200114860032L } },
                                { "Veteran 8", new List<long>() { 2761191524925440L } },
                                { "Veteran 16", new List<long>() { 2761208704794624L } },
                            }
                        }
                    };
                    break;
                case "Explosive Conflict":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Zorn & Toth", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 2788331423268864L, 2788335718236160L } },
                                { "Story 16", new List<long>() { 2860770341683200L, 2860766046715904L } },
                                { "Veteran 8", new List<long>() { 2857544821243904L, 2857549116211200L } },
                                { "Veteran 16", new List<long>() { 2861388816973824L, 2861384522006528L } },
                            }
                        },
                        {
                            "Firebrand & Stormcaller", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 2808827007205376L, 2808831302172672L } },
                                { "Story 16", new List<long>() { 2876459857215488L, 2876464152182784L } },
                                { "Veteran 8", new List<long>() { 2876434087411712L, 2876438382379008L } },
                                { "Veteran 16", new List<long>() { 2876481332051968L, 2876485627019264L } },
                            }
                        },
                        {
                            "Colonel Vorgath", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 2783478110224384L, 2848692893646848L } },
                                { "Story 16", new List<long>() { 2854156092047360L, 2854224811524096L } },
                                { "Veteran 8", new List<long>() { 2854151797080064L, 2854117437341696L } },
                                { "Veteran 16", new List<long>() { 2854160387014656L, 2854229106491392L } },
                            }
                        },
                        {
                            "Warlord Kephess", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() {  2800357331697664L,2794185463693312L} },
                                { "Story 16", new List<long>() { 2876550051528704L,2876562936430592L } },
                                { "Veteran 8", new List<long>() {  2876528576692224L,2876532871659520L } },
                                { "Veteran 16", new List<long>() {  2876588706234368L,2876593001201664L } },
                            }
                        }
                    };
                    break;
                case "Terror From Beyond":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "The Writhing Horror", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 2938874321960960L } },
                                { "Story 16", new List<long>() { 3010428477112320L } },
                                { "Veteran 8", new List<long>() { 3010424182145024L } },
                                { "Veteran 16", new List<long>() { 3010432772079616L } },
                            }
                        },                        {
                            "Dreadful Entity", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3057806261354496L } },
                                { "Story 16", new List<long>() { 3057806261354496L } },
                                { "Veteran 8", new List<long>() { 3057806261354496L } },
                                { "Veteran 16", new List<long>() { 3057806261354496L} },
                            }
                        },
                        {
                            "The Dread Guard", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 2938011033534464L, 2938006738567168L, 2938002443599872L } },
                                { "Story 16", new List<long>() { 3013387709579264L, 3013379119644672L, 3013374824677376L } },
                                { "Veteran 8", new List<long>() { 3013336169971712L, 3013331875004416L, 3013327580037120L } },
                                { "Veteran 16", new List<long>() { 3013413479383040L, 3013409184415744L, 3013417774350336L } },
                            }
                        },
                        {
                            "Operator IX", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 2942606648541184L } },
                                { "Story 16", new List<long>() { 2994850630729728L } },
                                { "Veteran 8", new List<long>() { 2994837745827840L } },
                                { "Veteran 16", new List<long>() { 2994859220664320L } },
                            }
                        },
                        {
                            "Kephess the Undying", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 2937620191510528L } },
                                { "Story 16", new List<long>() { 3013134306508800L } },
                                { "Veteran 8", new List<long>() { 3013121421606912L } },
                                { "Veteran 16", new List<long>() { 3013138601476096L } },
                            }
                        }
                        ,
                        {
                            "The Terror From Beyond", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 2938891501830144L, 2938895796797440L, 2938887206862848L, 2978340776443904, 2988545618739200 } },
                                { "Story 16", new List<long>() { 3025263294152704L, 3025276179054592L, 3025224639447040L, 3025237524348928L, 3025289063956480 } },
                                { "Veteran 8", new List<long>() { 3025271884087296L, 3025258999185408L, 3025220344479744L, 3025233229381632L, 3025284768989184L } },
                                { "Veteran 16", new List<long>() { 3025280474021888L, 3025267589120000L, 3025228934414336L, 3025241819316224L, 3025293358923776L } },
                            }
                        }
                    };
                    encounter.RequiredIdsForKill = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "The Terror From Beyond", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3009943145807872L } },
                                { "Story 16", new List<long>() { 3014925307871232L } },
                                { "Veteran 8", new List<long>() { 3014921012903936L } },
                                { "Veteran 16", new List<long>() { 3014929602838528L } },
                            } }
                    };
                    break;
                case "Scum and Villainy":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Dash'Roode", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3058837053505536L } },
                                { "Story 16", new List<long>() { 3153571147153408L } },
                                { "Veteran 8", new List<long>() { 3153558262251520L } },
                                { "Veteran 16", new List<long>() { 3153575442120704L } },
                            }
                        },
                        {
                            "Titan 6", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3016450021261312L } },
                                { "Story 16", new List<long>() { 3152463045591040L } },
                                { "Veteran 8", new List<long>() { 3152458750623744L } },
                                { "Veteran 16", new List<long>() { 3152467340558336L} },
                            }
                        },{
                            "Hateful Entity", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3264737785675776L } },
                                { "Story 16", new List<long>() { 3264737785675776L } },
                                { "Veteran 8", new List<long>() { 3264737785675776L } },
                                { "Veteran 16", new List<long>() { 3264737785675776L} },
                            }
                        },
                        {
                            "Thrasher", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3045819007631360L} },
                                { "Story 16", new List<long>() { 3154567579566080L } },
                                { "Veteran 8", new List<long>() { 3154563284598784L } },
                                { "Veteran 16", new List<long>() { 3154571874533376L } },
                            }
                        },
                        {
                            "Operations Chief", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3141940375715840L } },
                                { "Story 16", new List<long>() { 3157552581836800L } },
                                { "Veteran 8", new List<long>() { 3157548286869504L } },
                                { "Veteran 16", new List<long>() { 3157556876804096L } },
                            }
                        },
                        {
                            "Olok the Shadow", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3016445726294016L } },
                                { "Story 16", new List<long>() { 3154674953748480L } },
                                { "Veteran 8", new List<long>() { 3154662068846592L} },
                                { "Veteran 16", new List<long>() { 3154679248715776L } },
                            }
                        },
                        {
                            "Cartel Warlords", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3054413237190656L, 3054400352288768L, 3054404647256064L, 3054408942223360L } },
                                { "Story 16", new List<long>() { 3156899746807808L, 3054400352288768L, 3054404647256064L, 3054408942223360L } },
                                { "Veteran 8", new List<long>() { 3156895451840512L, 3054400352288768L, 3054404647256064L, 3054408942223360L } },
                                { "Veteran 16", new List<long>() { 3156904041775104L, 3054400352288768L, 3054404647256064L, 3054408942223360L } },
                            }
                        },
                        {
                            "Dread Master Styrak", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3066945951760384L, 3067057620910080,3225679353085952} },
                                { "Story 16", new List<long>() { 3152441570754560L, 3067057620910080,3225679353085952 } },
                                { "Veteran 8", new List<long>() { 3152407211016192L, 3067057620910080,3225679353085952 } },
                                { "Veteran 16", new List<long>() { 3152445865721856L, 3067057620910080,3225679353085952} },
                            }
                        }
                    };
                    break;
                case "The Dread Fortress":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Nefra, Who Bars the Way", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3266533082005504L } },
                                { "Story 16", new List<long>() { 3303036009054208L } },
                                { "Veteran 8", new List<long>() { 3303031714086912L } },
                                { "Veteran 16", new List<long>() { 3303040304021504L } },
                            }
                        },
                        {
                            "Gate Commander Draxus", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3273924720721920L} },
                                { "Story 16", new List<long>() { 3303401081274368L} },
                                { "Veteran 8", new List<long>() { 3303392491339776L } },
                                { "Veteran 16", new List<long>() { 3303405376241664L } },
                            }
                        },
                        {
                            "Grob'thok, Who Feeds the Forge", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3273929015689216L } },
                                { "Story 16", new List<long>() { 3302563562651648L } },
                                { "Veteran 8", new List<long>() { 3302559267684352L } },
                                { "Veteran 16", new List<long>() { 3302567857618944L } },
                            }
                        },
                        {
                            "Corruptor Zero", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3273933310656512L} },
                                { "Story 16", new List<long>() { 3303542815195136L } },
                                { "Veteran 8", new List<long>() { 3303534225260544L } },
                                { "Veteran 16", new List<long>() { 3303551405129728L } },
                            }
                        }
                        ,
                        {
                            "Dread Master Brontes", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3273937605623808L,3275625527771136L, 3277721471811584L } },
                                { "Story 16", new List<long>() { 3303538520227840L,3275625527771136L, 3277721471811584L } },
                                { "Veteran 8", new List<long>() { 3303529930293248L,3275625527771136L, 3277721471811584L } },
                                { "Veteran 16", new List<long>() { 3303547110162432L ,3275625527771136L, 3277721471811584L} },
                            }
                        }
                    };
                    encounter.RequiredIdsForKill = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Dread Master Brontes", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3273937605623808L} },
                                { "Story 16", new List<long>() { 3303538520227840L} },
                                { "Veteran 8", new List<long>() { 3303529930293248L} },
                                { "Veteran 16", new List<long>() { 3303547110162432L} },
                            }
                        }
                    };
                    break;
                case "The Dread Palace":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Dread Master Bestia", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3273941900591104L } },
                                { "Story 16", new List<long>() { 3273941900591104L } },
                                { "Veteran 8", new List<long>() { 3273941900591104L } },
                                { "Veteran 16", new List<long>() { 3273941900591104L } },
                            }
                        },
                        {
                            "Dread Master Tyrans", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3273954785492992L} },
                                { "Story 16", new List<long>() { 3273954785492992L} },
                                { "Veteran 8", new List<long>() { 3273954785492992L } },
                                { "Veteran 16", new List<long>() { 3273954785492992L } },
                            }
                        },
                        {
                            "Dread Master Calphayus", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3312983153311744L,3284949901770752L, 3284954196738048L, 3273946195558400L, 3349812497874944L } },
                                { "Story 16", new List<long>() { 3312991743246336L,3284949901770752L, 3284954196738048L, 3273946195558400L, 3349812497874944L } },
                                { "Veteran 8", new List<long>() { 3312987448279040L,3284949901770752L, 3284954196738048L, 3273946195558400L, 3349812497874944L } },
                                { "Veteran 16", new List<long>() { 3312996038213632L,3284949901770752L, 3284954196738048L, 3273946195558400L, 3349812497874944L } },
                            }
                        },
                        {
                            "Dread Master Raptus", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3273950490525696L} },
                                { "Story 16", new List<long>() { 3303555700097024L } },
                                { "Veteran 8", new List<long>() { 3302902865068032L } },
                                { "Veteran 16", new List<long>() { 3303559995064320L } },
                            }
                        }
                        ,
                        {
                            "Dread Council", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() {3273984850264064L,3273997735165952L, 3273989145231360L,3273993440198656L,3274019210002432L,3303830578003968L } },
                                { "Story 16", new List<long>() {3273984850264064L,3273997735165952L, 3273989145231360L,3273993440198656L,3274019210002432L,3303830578003968L} },
                                { "Veteran 8", new List<long>() {3273984850264064L,3273997735165952L, 3273989145231360L,3273993440198656L,3274019210002432L,3303830578003968L} },
                                { "Veteran 16", new List<long>() {3273984850264064L,3273997735165952L, 3273989145231360L,3273993440198656L,3274019210002432L,3303830578003968L} },
                            }
                        }
                    };
                    encounter.RequiredIdsForKill = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Dread Master Bestia", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3313331045662720L } },
                                { "Story 16", new List<long>() { 3313335340630016L } },
                                { "Veteran 8", new List<long>() { 3313339635597312L } },
                                { "Veteran 16", new List<long>() { 3313343930564608L } },
                            }
                        },
                        {
                            "Dread Master Tyrans", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3312841419390976L} },
                                { "Story 16", new List<long>() { 3312850009325568L } },
                                { "Veteran 8", new List<long>() { 3312845714358272L } },
                                { "Veteran 16", new List<long>() { 3312854304292864L } },
                            }
                        },
                        {
                            "Dread Master Calphayus", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3312983153311744L } },
                                { "Story 16", new List<long>() { 3312991743246336L } },
                                { "Veteran 8", new List<long>() { 3312987448279040L } },
                                { "Veteran 16", new List<long>() { 3312996038213632L } },
                            }
                        },
                        {
                            "Dread Master Raptus", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3313279506055168L } },
                                { "Story 16", new List<long>() { 3313288095989760L } },
                                { "Veteran 8", new List<long>() { 3313283801022464L } },
                                { "Veteran 16", new List<long>() { 3313292390957056L } },
                            }
                        }
                        ,
                        {
                            "Dread Council", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3302645167030272L } },
                                { "Story 16", new List<long>() { 3303426851078144L } },
                                { "Veteran 8", new List<long>() { 3303422556110848L } },
                                { "Veteran 16", new List<long>() { 3303431146045440L } },
                            }
                        }
                    };
                    break;
                case "The Ravagers":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Sparky", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3367555007774720L } },
                                { "Story 16", new List<long>() { 3458114393210880L } },
                                { "Veteran 8", new List<long>() { 3458110098243584L } },
                                { "Veteran 16", new List<long>() { 3458122983145472L } },
                            }
                        },
                        {
                            "Quartermaster Bulo", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3371446248144896L} },
                                { "Story 16", new List<long>() { 3468701487595520L} },
                                { "Veteran 8", new List<long>() { 3468705782562816L } },
                                { "Veteran 16", new List<long>() { 3468697192628224L } },
                            }
                        },
                        {
                            "Torque", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3397005598523392L } },
                                { "Story 16", new List<long>() { 3468714372497408L } },
                                { "Veteran 8", new List<long>() { 3468710077530112L } },
                                { "Veteran 16", new List<long>() { 3468718667464704L } },
                            }
                        },
                        {
                            "Master & Blaster", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3391095723524096L,3391100018491392L} },
                                { "Story 16", new List<long>() { 3462181727240192L,3391100018491392L } },
                                { "Veteran 8", new List<long>() { 3458148752949248L,3391100018491392L } },
                                { "Veteran 16", new List<long>() { 3462186022207488L,3391100018491392L } },
                            }
                        }
                        ,
                        {
                            "Coratanni", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3443983950807040L,3371437658210304L, 3371441953177600L } },
                                { "Story 16", new List<long>() { 3468735847333888L,3371437658210304L, 3371441953177600L } },
                                { "Veteran 8", new List<long>() { 3468731552366592L,3371437658210304L, 3371441953177600L } },
                                { "Veteran 16", new List<long>() { 3468740142301184L ,3371437658210304L, 3371441953177600L} },
                            }
                        }
                    };
                    encounter.RequiredIdsForKill = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Sparky", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3367555007774720L } },
                                { "Story 16", new List<long>() { 3458114393210880L } },
                                { "Veteran 8", new List<long>() { 3458110098243584L } },
                                { "Veteran 16", new List<long>() { 3458122983145472L } },
                            }
                        },
                        {
                            "Quartermaster Bulo", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3371446248144896L} },
                                { "Story 16", new List<long>() { 3468701487595520L} },
                                { "Veteran 8", new List<long>() { 3468705782562816L } },
                                { "Veteran 16", new List<long>() { 3468697192628224L } },
                            }
                        },
                        {
                            "Torque", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3397005598523392L } },
                                { "Story 16", new List<long>() { 3468714372497408L } },
                                { "Veteran 8", new List<long>() { 3468710077530112L } },
                                { "Veteran 16", new List<long>() { 3468718667464704L } },
                            }
                        },
                        {
                            "Master & Blaster", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3391095723524096L} },
                                { "Story 16", new List<long>() { 3462181727240192L} },
                                { "Veteran 8", new List<long>() { 3458148752949248L} },
                                { "Veteran 16", new List<long>() { 3462186022207488L} },
                            }
                        }
                        ,
                        {
                            "Coratanni", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3443983950807040 } },
                                { "Story 16", new List<long>() { 3468731552366592 } },
                                { "Veteran 8", new List<long>() { 3468735847333888 } },
                                { "Veteran 16", new List<long>() { 3468740142301184 } },
                            }
                        }
                    };
                    break;
                case "Temple of Sacrifice":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Malaphar the Savage", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3431245077807104L } },
                                { "Story 16", new List<long>() { 3469281308180480L } },
                                { "Veteran 8", new List<long>() { 3469277013213184L } },
                                { "Veteran 16", new List<long>() { 3469285603147776L } },
                            }
                        },
                        {
                            "Sword Squadron", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3447784996864000L,3447789291831296L} },
                                { "Story 16", new List<long>() { 3468770207072256L,3447789291831296L} },
                                { "Veteran 8", new List<long>() { 3468765912104960L,3447789291831296L } },
                                { "Veteran 16", new List<long>() { 3468774502039552L,3447789291831296L } },
                            }
                        },
                        {
                            "The Underlurker", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3411402328899584L } },
                                { "Story 16", new List<long>() { 3462267626586112L } },
                                { "Veteran 8", new List<long>() { 3462263331618816L } },
                                { "Veteran 16", new List<long>() { 3462271921553408L } },
                            }
                        },
                        {
                            "Revanite Commanders", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3482806160195584L,3456890327531520L, 3456894622498816L, 3456898917466112L} },
                                { "Story 16", new List<long>() { 3483029498494976L,3456890327531520L, 3456894622498816L, 3456898917466112L } },
                                { "Veteran 8", new List<long>() { 3483025203527680L,3456890327531520L, 3456894622498816L, 3456898917466112L } },
                                { "Veteran 16", new List<long>() { 3483033793462272L,3456890327531520L, 3456894622498816L, 3456898917466112L } },
                            }
                        }
                        ,
                        {
                            "Revan", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3431605855059968L, 3444310368321536L,3447583133401088L,3440805675008000L } },
                                { "Story 16", new List<long>() { 3431605855059968L, 3444310368321536L,3447583133401088L,3440805675008000L } },
                                { "Veteran 8", new List<long>() { 3431605855059968L, 3444310368321536L,3447583133401088L,3440805675008000L } },
                                { "Veteran 16", new List<long>() { 3431605855059968L, 3444310368321536L,3447583133401088L,3440805675008000L} },
                            }
                        }
                    };
                    encounter.RequiredIdsForKill = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Malaphar the Savage", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3431245077807104L } },
                                { "Story 16", new List<long>() { 3469281308180480L } },
                                { "Veteran 8", new List<long>() { 3469277013213184L } },
                                { "Veteran 16", new List<long>() { 3469285603147776L } },
                            }
                        },
                        {
                            "Sword Squadron", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3447784996864000L,3447789291831296L} },
                                { "Story 16", new List<long>() { 3468770207072256L,3447789291831296L} },
                                { "Veteran 8", new List<long>() { 3468765912104960L,3447789291831296L } },
                                { "Veteran 16", new List<long>() { 3468774502039552L,3447789291831296L } },
                            }
                        },
                        {
                            "The Underlurker", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3411402328899584L } },
                                { "Story 16", new List<long>() { 3462267626586112L } },
                                { "Veteran 8", new List<long>() { 3462263331618816L } },
                                { "Veteran 16", new List<long>() { 3462271921553408L } },
                            }
                        },
                        {
                            "Revanite Commanders", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3482806160195584L,3456890327531520L, 3456894622498816L, 3456898917466112L} },
                                { "Story 16", new List<long>() { 3483029498494976L,3456890327531520L, 3456894622498816L, 3456898917466112L } },
                                { "Veteran 8", new List<long>() { 3483025203527680L,3456890327531520L, 3456894622498816L, 3456898917466112L } },
                                { "Veteran 16", new List<long>() { 3483033793462272L,3456890327531520L, 3456894622498816L, 3456898917466112L } },
                            }
                        }
                        ,
                        {
                            "Revan", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() {3444310368321536L,3447583133401088L,3440805675008000L } },
                                { "Story 16", new List<long>() { 3444310368321536L,3447583133401088L,3440805675008000L } },
                                { "Veteran 8", new List<long>() { 3444310368321536L,3447583133401088L,3440805675008000L } },
                                { "Veteran 16", new List<long>() { 3444310368321536L,3447583133401088L,3440805675008000L} },
                            }
                        }
                    };
                    break;
                case "Toborro's Palace":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Golden Fury", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3210174521147392L } },
                                { "Story 16", new List<long>() { 3232800408862720L } },
                                { "Veteran 8", new List<long>() { 3232735984353280L } },
                                { "Veteran 16", new List<long>() { 3232817588731904L } },
                            }
                        }
                    };
                    break;
                case "Xeno":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Xenoanalyst II", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3153545377349632L } },
                                { "Story 16", new List<long>() { 3213924027596800L } },
                                { "Veteran 8", new List<long>() { 3213919732629504L } },
                                { "Veteran 16", new List<long>() { 3213928322564096L } },
                            }
                        }
                    };
                    break;
                case "Eyeless":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "The Eyeless", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3319090596806656L } },
                                { "Story 16", new List<long>() { 3328376316100608L } },
                                { "Veteran 8", new List<long>() { 3328372021133312L } },
                                { "Veteran 16", new List<long>() { 3328380611067904L } },
                            }
                        }
                    };
                    break;
                case "Queen's Hive":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Mutated Geonosian Queen", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 4197299739688960L } },
                                { "Story 16", new List<long>() { 4204768687816704L } },
                                { "Veteran 8", new List<long>() { 4204764392849408L } },
                                { "Veteran 16", new List<long>() { 4204772982784000L } },
                            }
                        }
                    };
                    break;
                case "Propagator Core":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Propagator Core XR-53", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 4824927605620736L } },
                                { "Veteran 8", new List<long>() { 4847325860069376L } },
                                { "Master 8", new List<long>() { 4847325860069376L,4861868619333632L,4831241207545856L, 4845470434197504L,4831722243883008L, 4831533265321984L, 4829566170300416L} },
                            }
                        }
                    };
                    break;
                case "Colossal Monolith":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Colossal Monolith", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3541140406009856L } },
                                { "Story 16", new List<long>() { 3570951774011392L } },
                                { "Veteran 8", new List<long>() { 3570947479044096L } },
                                { "Veteran 16", new List<long>() { 3570956068978688L } },
                            }
                        }
                    };
                    break;
                case "The Gods from the Machine":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "TYTH", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 4078427929837568L } },
                                { "Story 16", new List<long>() { 4078419339902976L } },
                                { "Veteran 8", new List<long>() { 4078423634870272L } },
                                { "Veteran 16", new List<long>() { 4078415044935680L } },
                            }
                        },
                        {
                            "Aivela and Esne", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 4088525397950464L,4088538282852352L,4101564918661120L} },
                                { "Story 16", new List<long>() { 4088525397950464L, 4088538282852352L,4101564918661120L} },
                                { "Veteran 8", new List<long>() { 4088525397950464L, 4088538282852352L,4101564918661120L } },
                                { "Veteran 16", new List<long>() { 4088525397950464L, 4088538282852352L,4101564918661120L } },
                            }
                        },
                        {
                            "NAHUT", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 4108011664572416L } },
                                { "Story 16", new List<long>() { 4122953855795200L } },
                                { "Veteran 8", new List<long>() { 4122949560827904L } },
                                { "Veteran 16", new List<long>() { 4122958150762496L } },
                            }
                        },
                        {
                            "SCYVA", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 4108140513591296L,4126527268585472L, 4126587398127616L } },
                                { "Story 16", new List<long>() { 4108140513591296L,4126527268585472L, 4158791062913024L } },
                                { "Veteran 8", new List<long>() {4108140513591296L, 4126527268585472L, 4158786767945728L } },
                                { "Veteran 16", new List<long>() { 4108140513591296L,4126527268585472L, 4158859782389760L } },
                                { "Master 8", new List<long>() {4108140513591296L, 4126527268585472L, 4158786767945728L } },
                                { "Master 16", new List<long>() {4108140513591296L,4126527268585472L, 4158859782389760L } },
                            }
                        }
                        ,
                        {
                            "IZAX", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 4108097563918336L } },
                                { "Story 16", new List<long>() { 4163128979881984L,4108097563918336L } },
                                { "Veteran 8", new List<long>() { 4163133274849280L,4108097563918336L} },
                                { "Veteran 16", new List<long>() {4163137569816576L,4108097563918336L} },
                            }
                        }
                    };
                    encounter.RequiredIdsForKill = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "SCYVA", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 4126587398127616L } },
                                { "Story 16", new List<long>() { 4158791062913024L } },
                                { "Veteran 8", new List<long>() { 4158786767945728L } },
                                { "Veteran 16", new List<long>() { 4158859782389760L } },
                                { "Master 8", new List<long>() { 4158786767945728L } },
                                { "Master 16", new List<long>() { 4158859782389760L } },
                            }
                        },
                                                {
                            "IZAX", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 4163124684914688L } },
                                { "Story 16", new List<long>() { 4163133274849280L } },
                                { "Veteran 8", new List<long>() {  4163128979881984L } },
                                { "Veteran 16", new List<long>() {  4163137569816576L } },
                                { "Master 8", new List<long>() {  4163128979881984L } },
                                { "Master 16", new List<long>() {  4163137569816576L } }
                            }
                        }
                    };
                    break;
                case "Dxun":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Red", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 4246176467517440L } },
                                { "Story 16", new List<long>() { 4246176467517440L } },
                                { "Veteran 8", new List<long>() { 4330233272467456L } },
                                { "Veteran 16", new List<long>() { 4330233272467456L } },
                            }
                        },
                        {
                            "Holding Pens I", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 4245686841245696L, 4340618503389184L } },
                                { "Story 16", new List<long>() { 4245686841245696L, 4340618503389184L } },
                                { "Veteran 8", new List<long>() { 4245686841245696L, 4340614208421888L } },
                                { "Veteran 16", new List<long>() { 4245686841245696L , 4340614208421888L } },
                                { "Master 8",new List<long>(){ 4245686841245696L, 4389417921806336L,4340614208421888 }},
                                { "Master 16",new List<long>(){ 4245686841245696L, 4389417921806336L,4340614208421888}}
                            }
                        },
                        {
                            "Holding Pens II", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 4245686841245696L, 4330082948612096L } },
                                { "Story 16", new List<long>() { 4245686841245696L, 4330082948612096L } },
                                { "Veteran 8", new List<long>() { 4245686841245696L, 4330061473775616L } },
                                { "Veteran 16", new List<long>() { 4245686841245696L, 4330061473775616L } },
                                { "Master 8",new List<long>(){ 4245686841245696L, 4390577562976256L,4330061473775616L}},
                                { "Master 16",new List<long>(){ 4245686841245696L, 4390577562976256L,4330061473775616L}}
                            }
                        },
                        {
                            "Trandosians", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 4245970309087232L,4245978899021824L, 4245983193989120L, 4245987488956416L} },
                                { "Story 16", new List<long>() { 4245970309087232L,4245978899021824L, 4245983193989120L, 4245987488956416L} },
                                { "Veteran 8", new List<long>() { 4245970309087232L,4245978899021824L, 4245983193989120L, 4245987488956416L } },
                                { "Veteran 16", new List<long>() { 4245970309087232L,4245978899021824L, 4245983193989120L, 4245987488956416L } },
                                { "Master 8",new List<long>(){4381150109761536L,4245970309087232L,4245978899021824L, 4245983193989120L, 4245987488956416L}},
                                { "Master 16",new List<long>(){4381150109761536L,4245970309087232L,4245978899021824L, 4245983193989120L, 4245987488956416L}}
                            }
                        },
                        {
                            "Huntmaster", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 4265104388390912L ,4281661487316992L} },
                                { "Story 16", new List<long>() { 4265104388390912L,4281661487316992L } },
                                { "Veteran 8", new List<long>() { 4265104388390912L ,4330237567434752L} },
                                { "Veteran 16", new List<long>() { 4265104388390912L ,4330237567434752L} },
                            }
                        },
                        {
                            "Apex Vanguard", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 4282872668094464L} },
                                { "Story 16", new List<long>() { 4282872668094464L } },
                                { "Veteran 8", new List<long>() { 4350020186800128 } },
                                { "Veteran 16", new List<long>() { 4350020186800128 } },
                            }
                        }
                    };
                    encounter.RequiredIdsForKill = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Red", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 4246176467517440L } },
                                { "Story 16", new List<long>() { 4246176467517440L } },
                                { "Veteran 8", new List<long>() { 4330233272467456L } },
                                { "Veteran 16", new List<long>() { 4330233272467456L } },
                            }
                        },
                        {
                            "Holding Pens I", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 4340618503389184L } },
                                { "Story 16", new List<long>() { 4340618503389184L } },
                                { "Veteran 8", new List<long>() { 4340614208421888L } },
                                { "Veteran 16", new List<long>() { 4340614208421888L } },
                                { "Master 8",new List<long>(){ 4340614208421888L}},
                                { "Master 16",new List<long>(){ 4340614208421888L}}
                            }
                        },
                        {
                            "Holding Pens II", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 4330082948612096L } },
                                { "Story 16", new List<long>() { 4330082948612096L } },
                                { "Veteran 8", new List<long>() { 4330061473775616L } },
                                { "Veteran 16", new List<long>() { 4330061473775616L } },
                                { "Master 8",new List<long>(){ 4330061473775616L}},
                                { "Master 16",new List<long>(){ 4330061473775616L}}
                            }
                        },
                        {
                            "Huntmaster", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 4281661487316992L } },
                                { "Story 16", new List<long>() { 4281661487316992L } },
                                { "Veteran 8", new List<long>() { 4330237567434752L} },
                                { "Veteran 16", new List<long>() { 4330237567434752L} },
                            }
                        },
                        {
                            "Apex Vanguard", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 4282872668094464L} },
                                { "Story 16", new List<long>() { 4282872668094464L } },
                                { "Veteran 8", new List<long>() { 4350020186800128 } },
                                { "Veteran 16", new List<long>() { 4350020186800128 } },
                            }
                        }
                    };
                    encounter.RequiredAbilitiesForKill = new Dictionary<string, Dictionary<string, ulong>>()
                    {
                        {
                            "Trandosians", new Dictionary<string, ulong>()
                            {
                                {"Master 8", 4381085685252096},
                                {"Master 16",4381085685252096}
                            }
                        }
                    };
                    break;
                case "R4":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "IP-CPT", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 4467247024177152L,4480196350574592 ,4480200645541888} },
                                { "Veteran 8", new List<long>() { 4494653210492928L ,4480196350574592 ,4480200645541888} },
                            }
                        },
                        {
                            "Watchdog", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 4466177577320448L} },
                                { "Veteran 8", new List<long>() { 4494700455133184L } },
                            }
                        },
                        {
                            "Lord Kanoth", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 4466190462222336L } },
                                { "Veteran 8", new List<long>() { 4494876548792320L } },
                            }
                        },
                        {
                            "Lady Dominique", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 4466199052156928L} },
                                { "Veteran 8", new List<long>() { 4608014577303552L,4488524292161536 } },
                            }
                        }
                    };
                    encounter.RequiredIdsForKill = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Lady Dominique", new Dictionary<string, List<long>>()
                            {
                                {"Story 8", new List<long>() { 4466199052156928L}},
                                {"Veteran 8",new List<long>() { 4608014577303552L}}
                            }
                        }
                    };
                    break;
                //////////FLASHPOINTS///////////
                case "The Black Talon":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Sergeant Boran", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 425970561449984} },
                                { "Master 4", new List<long>() { 2509772729352192} }
                            }
                        }
                        ,
                        {
                            "GXR-5 Sabotage Droid", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 430896888938496} },
                                { "Master 4", new List<long>() { 2509699714908160} }
                            }
                        },
                        {
                            "GXR-7 Command Droid", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 2509755549483008 } }
                            }
                        },
                        {
                            "Commander Ghulil", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 364857471795200} },
                                { "Master 4", new List<long>() { 2509686830006272} }
                            }
                        },
                        {
                            "Yadira Ban", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 365755119960064} },
                                { "Master 4", new List<long>() { 2509777024319488} }
                            }
                        }
                    };
                    break;
                case "The Esseles":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Ironfist", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 649484954501120} },
                                { "Master 4", new List<long>() { 2510769161764864} }
                            }
                        },
                        {
                            "Lieutenant Isric", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 649467774631936} },
                                { "Master 4", new List<long>() { 2510773456732160} }
                            }
                        },
                        {
                            "ISS-7 Guardian Battledroid", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 653359015002112} },
                                { "Master 4", new List<long>() { 2510777751699456} }
                            }
                        },
                        {
                            "ISS-944 Power Droid", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 2510782046666752} }
                            }
                        },
                        {
                            "Vokk", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 649472069599232} },
                                { "Master 4", new List<long>() { 2510790636601344} }
                            }
                        }
                    };
                    break;
                case "Directive 7":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Detector", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 1626340906237952 } },
                                { "Master 4", new List<long>() { 2512014702280704 } }
                            }
                        },
                        {
                            "Mentor Assasin Droids", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 746748783886336 , 746753078853632 , 746757373820928 } },
                                { "Master 4", new List<long>() { 2511958867705856 , 2511954572738560 , 2511963162673152 } }
                            }
                        },
                        {
                            "Interrogator", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 1627981583745024 } },
                                { "Master 4", new List<long>() { 2512053356986368 } }
                            }
                        },
                        {
                            "Bulwark", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 751348693860352 } },
                                { "Master 4", new List<long>() { 2511984637509632 } }
                            }
                        },
                        {
                            "Assembler", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 1628604354002944 } },
                                { "Master 4", new List<long>() { 2511976047575040 } }
                            }
                        },
                        {
                            "Replicator", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 1629334498443264 } },
                                { "Master 4", new List<long>() { 2512109191561216 } }
                            }
                        },
                        {
                            "Mentor", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 2144842243112960 } },
                                { "Master 4", new List<long>() { 2512100601626624 } }
                            }
                        }
                    };
                    break;
                case "Boarding Party":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "HXI-54 Juggernaut", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 745340034613248} },
                                { "Master 4", new List<long>() { 2514497193377792} }
                            }
                        },
                        {
                            "Sakan Do'nair", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 739816706670592} },
                                { "Master 4", new List<long>() { 2514522963181568} }
                            }
                        },
                        {
                            "Chief Engineer Kels", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 1004111814197248} },
                                { "Master 4", new List<long>() { 2514488603443200} }
                            }
                        },
                        {
                            "Officer Trio", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 957597318381568,739825296605184,957850721452032} },
                                { "Master 4", new List<long>() { 2514527258148864,2514531553116160,2514535848083456} }
                            }
                        }
                    };
                    break;
                case "The Foundry":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Foundry Guardian", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 747710856560640 } },
                                { "Master 4", new List<long>() { 2514742006513664 } }
                            }
                        },
                        {
                            "N4-10 Exterminator", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 1169322026205184 } },
                                { "Master 4", new List<long>() { 2514729121611776 } }
                            }
                        },
                        {
                            "Burrower Matriarch", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 1148233736781824 } },
                                { "Master 4", new List<long>() { 2514724826644480 } }
                            }
                        },
                        {
                            "HK-47", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 739833886539776 } },
                                { "Master 4", new List<long>() { 2514754891415552 } }
                            }
                        },
                        {
                            "Revan", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 739846771441664 } },
                                { "Master 4", new List<long>() { 2514780661219328 } }
                            }
                        }
                    };
                    break;
                case "Hammer Station":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "DN-314 Tunneler", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 2276525940408320} },
                                { "Master 4", new List<long>() { 3152170987814912} }
                            }
                        },
                        {
                            "Asteroid Beast", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 3251019660132352 } }
                            }
                        },
                        {
                            "Vorgan the Volcano", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 2286228271529984,2286241156431872,2286223976562688} },
                                { "Master 4", new List<long>() { 3152166692847616,3152158102913024,3152162397880320} }
                            }
                        },
                        {
                            "Battlelord Kreshan", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 3184361767698432} },
                                { "Master 4", new List<long>() { 3172554902601728} }
                            }
                        }
                    };
                    break;
                case "Athiss":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Professor Ley'arsha", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 1172496007036928} },
                                { "Master 4", new List<long>() { 3158072272879616} }
                            }
                        },
                        {
                            "The Beast of Vodal Kressh", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 2263129937412096} },
                                { "Master 4", new List<long>() { 3158119517519872} }
                            }
                        },
                        {
                            "Ancient Abomination", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 3247459132243968} }
                            }
                        },
                        {
                            "Prophet of Vodal", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 1172951273570304} },
                                { "Master 4", new List<long>() { 3158123812487168} }
                            }
                        }
                    };
                    break;
                case "Mandalorian Raiders":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Braxx the Bloodhound", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 2438167034593280} },
                                { "Master 4", new List<long>() { 3158428755165184} }
                            }
                        },
                        {
                            "Republic Boarding Party", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 2442474886791168,2442483476725760,2442479181758464,2442487771693056} },
                                { "Master 4", new List<long>() { 3158398690394112,3158402985361408,3158411575296000,3158420165230592} }
                            }
                        },
                        {
                            "Imperial Boarding Party", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 2442466296856576,2442462001889280,2442457706921984,2442470591823872} },
                                { "Master 4", new List<long>() { 3158265546407936,3158394395426816,3158407280328704,3158415870263296} }
                            }
                        },
                        {
                            "Gil", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 3250538623795200} }
                            }
                        },
                        {
                            "Mavrix Varad", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 1276069143379968} },
                                { "Master 4", new List<long>() { 3172567787503616} }
                            }
                        }
                    };
                    break;
                case "Cademimu":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Officer Xander", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 2504232221540352,2504236516507648} },
                                { "Master 4", new List<long>() { 3210672737353728,3210668442386432} }
                            }
                        },
                        {
                            "Captain Grimyk", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 2503961638600704} },
                                { "Master 4", new List<long>() { 3210677032321024} }
                            }
                        },
                        {
                            "Garold the Dark One", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 3247463427211264} }
                            }
                        },
                        {
                            "General Ortol", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 2506547208912896} },
                                { "Master 4", new List<long>() { 3210659852451840} }
                            }
                        }
                    };
                    break;
                case "The Red Reaper":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Lord Kherus", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 2482594176303104} }
                            }
                        },
                        {
                            "SV-3 Eradicator", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 2482692960550912} }
                            }
                        },
                        {
                            "Darth Ikoral", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 1478800189685760} }
                            }
                        }
                    };
                    break;
                case "Taral V":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Captain Shivanek & Ripper", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 748179007995904,747891245187072} },
                                { "Master 4", new List<long>() { 2513870128152576,2513865833185280} }
                            }
                        },
                        {
                            "General Edikar", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 748187597930496} },
                                { "Master 4", new List<long>() { 2531994890141696} }
                            }
                        }
                    };
                    break;
                case "The Battle of Ilum":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Gark the Indomitable", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 1675608476090368} },
                                { "Master 4", new List<long>() { 2511477831368704} }
                            }
                        },
                        {
                            "Velasu Graege & Drinda-Zel", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 770942334664704,770938039697408} },
                                { "Master 4", new List<long>() { 2511490716270592,2511473536401408} }
                            }
                        },
                        {
                            "Krel Thak", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 770946629632000} },
                                { "Master 4", new List<long>() { 2511486421303296} }
                            }
                        }
                        ,
                        {
                            "Darth Serevin", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 770925154795520} },
                                { "Master 4", new List<long>() { 2511469241434112} }
                            }
                        }
                    };
                    break;
                case "The False Emperor":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Tregg the Destroyer", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 1690314444111872} },
                                { "Master 4", new List<long>() { 2511761299210240} }
                            }
                        },
                        {
                            "Jindo Krey", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 770959514533888} },
                                { "Master 4", new List<long>() { 2511709759602688} }
                            }
                        },
                        {
                            "Prototype A-14 & Prototype B-16", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 1790202498514944,1790206793482240} },
                                { "Master 4", new List<long>() { 2511718349537280,2511726939471872} }
                            }
                        }
                        ,
                        {
                            "HK-47", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 770955219566592} },
                                { "Master 4", new List<long>() { 2511701169668096} }
                            }
                        }
                        ,
                        {
                            "Sith Entity", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 2158341325324288} },
                                { "Master 4", new List<long>() { 2511744119341056} }
                            }
                        }

                        ,
                        {
                            "Darth Malgus", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 770963809501184} },
                                { "Master 4", new List<long>() { 2511688284766208} }
                            }
                        }
                    };
                    break;
                case "Kaon Under Siege":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Rakghoul Behemoth", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 850519488724992} },
                                { "Master 4", new List<long>() { 2765357643202560} }
                            }
                        },
                        {
                            "KR-82 Expulser", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 2765349053267968} },
                                { "Master 4", new List<long>() { 2765353348235264} }
                            }
                        },
                        {
                            "Commander Lk'graagth", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 2762127827795968,2762123532828672,2762140712697856} },
                                { "Master 4", new List<long>() { 2762260971782144,2762265266749440,2762269561716736} }

                            }
                        }
                    };
                    break;
                case "Lost Island":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Putrid Shaclaw", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 2838853123571712} },
                                { "Master 4", new List<long>() { 2898153737027584} },
                            }
                        },
                        {
                            "LR-5 Sentinel Droid", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 2808620848775168} },
                                {"Master 4",new List<long>(){2815832098865152}}
                            }
                        },
                        {
                            "Transgenic Sample Seven", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 2800490475683840} },
                                { "Master 4", new List<long>() { 2857059489939456} }
                            }
                        }
                        ,
                        {
                            "Project Sav-Rak", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 2802144038092800} },
                                { "Master 4", new List<long>() { 2819585900281856} }
                            }
                        }
                        ,
                        {
                            "Transgenic Sample Eleven", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 2802509110312960} },
                                { "Master 4", new List<long>() { 2835799401824256} }
                            }
                        }

                        ,
                        {
                            "Doctor Lorrick", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 2794163988856832,2809320928444416 } },
                                { "Master 4", new List<long>() { 2838582540632064,2819959562436608} }
                            }
                        }
                    };
                    encounter.RequiredIdsForKill = new Dictionary<string, Dictionary<string, List<long>>>
                    {
                        {"Doctor Lorrick", new Dictionary<string, List<long>>
                        {
                            {"Veteran 4", new List<long>{ 2809320928444416 } },
                            { "Master 4", new List<long>() {2819959562436608} }
                        } }
                    };
                    break;
                case "Czerka Corporate Labs":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "CZ-8X Eradicator Droid", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 3245981663494144} },
                                { "Master 4", new List<long>() { 3279297724809216} },
                            }
                        },
                        {
                            "Chief Zokar", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 3245985958461440} },
                                {"Master 4",new List<long>(){3279302019776512}}
                            }
                        },
                        {
                            "Rasmus Blys", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 3247210024140800} },
                                { "Master 4", new List<long>() { 3279306314743808} }
                            }
                        }
                    };
                    break;
                case "Czerka Core Meltdown":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Enhanced Duneclaw", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 3247274448650240} },
                                { "Master 4", new List<long>() { 3279293429841920} },
                            }
                        },
                        {
                            "Enhanced Vrblther", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 3247278743617536} },
                                {"Master 4",new List<long>(){3279289134874624}}
                            }
                        },
                        {
                            "The Vigilant", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 3247240088911872} },
                                { "Master 4", new List<long>() { 3279310609711104} }
                            }
                        }
                    };
                    break;
                case "Assault on Tython":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Major Imos", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 3327405653491712} },
                                { "Master 4", new List<long>() { 3465471672188928} },
                            }
                        },
                        {
                            "Major Travik", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 3331296893861888} },
                                {"Master 4",new List<long>(){3465682125586432}}
                            }
                        },
                        {
                            "Master Liam Dentiri", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 3325902414938112} },
                                { "Master 4", new List<long>() { 3465536096698368} }
                            }
                        }
                        ,
                        {
                            "Republic Commander", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 3493169916280832 } },
                                { "Master 4", new List<long>() { 3485688083251200 } }
                            }
                        }
                        ,
                        {
                            "Imperial Commander", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 3493174211248128 } },
                                { "Master 4", new List<long>() { 3485696673185792 } }
                            }
                        }
                        ,
                        {
                            "Lieutenant Kreshin", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 3328904597078016} },
                                { "Master 4", new List<long>() { 3465690715521024} }
                            }
                        }
                        ,
                        {
                            "Master Oric Traless", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 3327796495515648} },
                                { "Master 4", new List<long>() { 3465579046371328} }
                            }
                        }

                        ,
                        {
                            "Lord Goh", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 3325872350167040} },
                                { "Master 4", new List<long>() { 3465699305455616} }
                            }
                        }
                    };
                    break;
                case "Korriban Incursion":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Master Riilna", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 3333461557379072} },
                                { "Master 4", new List<long>() { 3468753027203072} },
                            }
                        },
                        {
                            "Lord Renning", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 3333474442280960} },
                                {"Master 4",new List<long>(){3468899056091136}}
                            }
                        },
                        {
                            "Republic Commander", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 3493182801182720 } },
                                { "Master 4", new List<long>() { 3484803319988224 } }
                            }
                        }
                        ,
                        {
                            "Imperial Commander", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 3493187096150016 } },
                                { "Master 4", new List<long>() { 3484816204890112 } }
                            }
                        }
                        ,
                        {
                            "R-9XR", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 3329905324457984} },
                                { "Master 4", new List<long>() { 3468748732235776} }
                            }
                        }
                        ,
                        {
                            "I5-T1", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 3334535299203072} },
                                { "Master 4", new List<long>() { 3468894761123840} }
                            }
                        }
                        ,
                        {
                            "Commander Jensyn", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 3331339843534848} },
                                { "Master 4", new List<long>() { 3468757322170368} }
                            }
                        }

                        ,
                        {
                            "Darth Soverus", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 3331378498240512} },
                                { "Master 4", new List<long>() { 3468911940993024} }
                            }
                        }
                    };
                    break;
                case "Depths of Manaan":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Sairisi", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 3350332188917760} },
                                { "Master 4", new List<long>() { 3505251659284480} },
                            }
                        },
                        {
                            "M2-AUX Foreman", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 3506896631758848} },
                                {"Master 4",new List<long>(){3506922401562624}}
                            }
                        },
                        {
                            "Ortuno", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 3350327893950464} },
                                {"Master 4",new List<long>(){3505260249219072}}
                            }
                        },
                        {
                            "Stivastin", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 3362925033029632} },
                                { "Master 4", new List<long>() { 3505268839153664} }
                            }
                        }
                    };
                    break;
                case "Legacy of the Rakata":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Savage War Beast and War Chief Rehkta", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 3370608729522176,3372962371600384} },
                                { "Master 4", new List<long>() { 3491898605961216,3491894310993920} },
                            }
                        },
                        {
                            "Infinite Army Prototype", new Dictionary<string, List<long>>()
                            {
                                {"All",new List<long>(){3493419024384000}}
                            }
                        },
                        {
                            "Commander Rand", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 3373078335717376} },
                                {"Master 4",new List<long>(){3491885721059328}}
                            }
                        },
                        {
                            "Darth Arkous and Colonel Darok", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 3370613024489472,3370617319456768} },
                                { "Master 4", new List<long>() { 3491890016026624,3491881426092032} }
                            }
                        }
                    };
                    break;
                case "Blood Hunt":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Kyramla Gemas'rugam", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 3400982738239488} },
                                { "Master 4", new List<long>() { 3507004005941248} },
                            }
                        },
                        {
                            "Valk & Jos", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 3384198006046720,3384193711079424} },
                                { "Master 4", new List<long>() { 3507034070712320,3507029775745024} },
                            }
                        },
                        {
                            "Shae Vizla", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 3407807441272832} },
                                {"Master 4",new List<long>(){3507051250581504}}
                            }
                        }
                    };
                    break;
                case "Battle of Rishi":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Rarrook and Marko Ka", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 3369139850706944,3369144145674240} },
                                { "Master 4", new List<long>() { 3533559788732416,3533564083699712} },
                            }
                        },
                        {
                            "Master Obai and Lord Vodd", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 3369148440641536,3369152735608832} },
                                { "Master 4", new List<long>() { 3533581263568896,3533585558536192} },
                            }
                        },
                        {
                            "Commander Mokan", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 3417131815272448} }
                            }
                        },
                        {
                            "Shield Squadron Unit 1", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() {3369157030576128} },
                                { "Master 4", new List<long>() { 3533594148470784} },
                            }
                        }
                    };
                    break;
                case "Crisis on Umbara":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Technician Canni & Elli-Vaa", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 4111507767951360,4111512062918656} }
                            }
                        },
                        {
                            "Alpha Slybex", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 4121025415479296} }
                            }
                        },
                        {
                            "Vixian Mauler", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 4112173487882240} }
                            }
                        },
                        {
                            "Umbaran Spider Tank", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 4112779078270976} }
                            }
                        }
                    };
                    break;
                case "A Traitor Among The Chiss":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Guardian Droid", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 4128382694457344} }
                            }
                        },
                        {
                            "Syndic Zenta", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 4135314771673088} }
                            }
                        },
                        {
                            "Strike Team Walker", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 4137311931465728} }
                            }
                        },
                        {
                            "Valss", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 4132209510318080} }
                            }
                        }
                    };
                    break;
                case "The Nathema Conspiracy":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Voreclaw", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 4157176155209728} }
                            }
                        },
                        {
                            "Hands of Zildrog", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 4172298735058944,4172294440091648} }
                            }
                        },
                        {
                            "Ancient Guardian Droid", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 4152640669745152} }
                            }
                        },
                        {
                            "GEMINI 16", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 4158834012585984} }
                            }
                        }
                        ,
                        {
                            "Vinn Atrius & Zildrog", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 4158872667291648,4159276394217472} }
                            }
                        }
                    };
                    break;
                case "Objective Meridian":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "R10-X6", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 4257467936538624} }
                            }
                        },
                        {
                            "Vulture Squad", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 4257498001309696,4275957770747904} }
                            }
                        },
                        {
                            "Commander Rasha", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 4258185196077056} }
                            }
                        },
                        {
                            "Lord Feng & Darth Yun", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 4276941318258688,4257506591244288} }
                            }
                        }
                        ,
                        {
                            "Master Jakir & Seldin", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 4257442166734848,4257455051636736} }
                            }
                        },
                        {
                            "Commander Aster", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 4258021987319808} }
                            }
                        },
                        {
                            "Tau Idair", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 4257463641571328} }
                            }
                        },
                        {
                            "Darth Malgus", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 4257502296276992} }
                            }
                        }
                    };
                    break;
                case "Spirit of Vengeance":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Gorga Brak", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 4419697441243136} }
                            }
                        },
                        {
                            "Goldie", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 4434373344493568} }
                            }
                        },
                        {
                            "Bask Sunn", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 4419710326145024} }
                            }
                        },
                        {
                            "Troya Ajak", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 4419714621112320} }
                            }
                        }
                        ,
                        {
                            "Heta Kol", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 4437744893820928,4419718916079616} }
                            }
                        }
                    };
                    encounter.RequiredIdsForKill = new Dictionary<string, Dictionary<string, List<long>>>
                    {
                        {
                            "Heta Kol", new Dictionary<string, List<long>>
                                {
                                { "All", new List<long>{ 4442409228304384L } }
                                }
                        }
                    };
                    break;
                case "Secrets of the Enclave":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Graul", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 4445312626196480} }
                            }
                        },
                        {
                            "Republic Squad", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 4445291151360000,4445282561425408,4445286856392704} }
                            }
                        },
                        {
                            "Imperial Squad", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 4445278266458112,4445295446327296,4445299741294592} }
                            }
                        },
                        {
                            "Monstrous Terentatek", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 4445316921163776} }
                            }
                        },
                        {
                            "Master Leeha Narezz", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 4445308331229184} }
                            }
                        }
                        ,
                        {
                            "Colonel Barden Golah", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 4445304036261888} }
                            }
                        }
                    };
                    break;
                case "Ruins of Nul":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Apex Ranphyx", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 4468097427701760} }
                            }
                        },
                        {
                            "Regnant", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 4469132514820096,4479955832406016} }
                            }
                        },
                        {
                            "Droid Squad", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 4563939622912000,4563948212846592,4563943917879296} }
                            }
                        },
                        {
                            "Darth Malgus", new Dictionary<string, List<long>>()
                            {
                                { "All", new List<long>() { 4468509744562176} }
                            }
                        }
                    };
                    break;
                case "Maelstrom Prison":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Colonel Daksh", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() {748153238192128} },
                                { "Master 4", new List<long>() { 2513608135147520} }
                            }
                        },
                        {
                            "Lord Kancras", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() {1007388874244096} },
                                { "Master 4", new List<long>() { 2513668264689664} }
                            }
                        },
                        {
                            "Ancient Maelstrom Flayer", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() {1450539304878080} },
                                { "Master 4", new List<long>() { 2513784228806656} }
                            }
                        },
                        {
                            "Lord Vanithrast", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() {748170418061312} },
                                { "Master 4", new List<long>() { 2513672559656960} }
                            }
                        },
                        {
                            "Grand Moff Kilran", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() {723873788067840} },
                                { "Master 4", new List<long>() { 2536938397499392} }
                            }
                        }
                    };
                    break;
                case "Shrine of Silence":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Corrupted Vorantikus", new Dictionary<string, List<long>>()
                            {
                                { "Story 4", new List<long>() { 4702735786049536 } },
                                { "Veteran 4", new List<long>() { 4702748670951424 } },
                                { "Master 4", new List<long>() { 4702752965918720 } }
                            }
                        },
                        {
                            "Voss Mystics", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 4705574759432192 , 4705600529235968 } },
                                { "Master 4", new List<long>() { 4705591939301376 , 4705596234268672 } }
                            }
                        },
                        {
                            "Kirba, the Forgotten", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 4702765850820608 } },
                                { "Master 4", new List<long>() { 4702770145787904 } }
                            }
                        },
                        {
                            "Nil-Uu", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 4699299812212736 } },
                                { "Master 4", new List<long>() { 4699299812212736 } }
                            }
                        },
                        {
                            "Soul Coil", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 4699119423586304 } },
                                { "Master 4", new List<long>() { 4699119423586304 } }
                            }
                        },
                                                {
                            "The Curse", new Dictionary<string, List<long>>()
                            {
                                { "Veteran 4", new List<long>() { 4705561874530304 } },
                                { "Master 4", new List<long>() { 4705566169497600 } }
                            }
                        }
                    };
                    break;
            }
        }
    }
}