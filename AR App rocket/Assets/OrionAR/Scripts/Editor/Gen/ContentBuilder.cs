using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OrionAR.Gen
{
    /// <summary>
    /// Authors the educational content as ScriptableObject assets.
    ///
    /// Each component carries a summary and a separate "why it matters" note. The
    /// split is deliberate: the summary says what the thing is, which is quickly
    /// forgotten, and the note says what constraint forced it to be that way, which
    /// is what people actually remember and repeat.
    ///
    /// The quiz is built on the physics in the supplied NASA source material -
    /// Newton's third law, mass fraction, staging, specific impulse, centre of mass
    /// versus centre of pressure - rather than on trivia, so it tests the concepts an
    /// instructor would expect to see assessed.
    /// </summary>
    public static class ContentBuilder
    {
        public const string DataFolder = "Assets/OrionAR/Data";
        public const string PartFolder = DataFolder + "/Parts";

        // ================================================================
        // Components
        // ================================================================

        public static Dictionary<string, PartInfo> BuildParts()
        {
            AssetPaths.EnsureFolder(PartFolder);
            var map = new Dictionary<string, PartInfo>();

            // ---- Exterior ------------------------------------------------
            Add(map, "heat_shield", "Ablative Heat Shield", "Heat Shield",
                PartZone.Exterior, 10,
                "5.03 m diameter  ·  AVCOAT ablator",
                "The largest heat shield ever flown on a crewed spacecraft. Returning from " +
                "the Moon, Orion meets the atmosphere at about 40,000 km/h and the gas ahead " +
                "of the shield reaches roughly 2,760\u00A0°C.\n\n" +
                "It is ablative, not insulating. The AVCOAT material — silica fibres in a " +
                "resin, held in a fibreglass honeycomb — is designed to char and erode away, " +
                "carrying the heat off with the departing material.",
                "The shield is consumed by design and cannot be reused. Protecting the vehicle " +
                "by throwing part of it away is cheaper in mass than any material that could " +
                "survive the heating intact.");

            Add(map, "back_shell", "Back Shell and Thermal Protection", "Back Shell",
                PartZone.Exterior, 20,
                "Blunt 57.5° cone  ·  ~1,300 tiles",
                "The cone covering everything above the heat shield. It does not face the " +
                "fiercest heating, but it must survive vacuum, micrometeoroid strikes and a " +
                "swing of hundreds of degrees between sunlight and shadow.",
                "The blunt shape is deliberately poor aerodynamics. A pointed nose would hug " +
                "its own shock wave; a blunt body pushes a detached bow shock well ahead of " +
                "the vehicle, so the atmosphere absorbs most of the entry energy instead of " +
                "the spacecraft.");

            Add(map, "side_hatch", "Side Hatch", "Side Hatch",
                PartZone.Exterior, 30,
                "Crew ingress and egress",
                "The main crew access hatch. It is how astronauts board on the pad and how " +
                "recovery teams reach them after splashdown.",
                "It is a plug design: cabin pressure pushes it harder into its seal rather " +
                "than out of it. With the cabin pressurised and vacuum outside, the hatch " +
                "cannot be opened at all. That is a structural property, not a procedure " +
                "someone has to remember.");

            Add(map, "windows", "Crew Module Windows", "Windows",
                PartZone.Exterior, 40,
                "Four windows  ·  multi-pane fused silica",
                "Orion carries four windows for docking and for monitoring the vehicle. Each " +
                "is a serious engineering problem: a hole in a pressure vessel that must also " +
                "survive re-entry heating.",
                "The outer pane is a thermal and debris shield. The inner panes are the real " +
                "pressure panes, and each one alone is strong enough to hold cabin pressure, " +
                "so a crack in any single pane cannot breach the cabin.");

            Add(map, "docking_system", "NASA Docking System", "Docking System",
                PartZone.Exterior, 50,
                "Androgynous  ·  International Docking Standard",
                "The forward bay cover protects the docking adapter and parachutes during " +
                "launch and re-entry. The mechanism beneath is androgynous — both spacecraft " +
                "carry an identical unit and either can be the active partner.",
                "Docking happens in two stages. Soft capture uses magnets and sprung latches " +
                "to absorb the residual closing velocity; only then does hard capture pull the " +
                "vehicles into a pressure-tight seal.");

            // ---- Service module ------------------------------------------
            Add(map, "service_module", "European Service Module", "Service Module",
                PartZone.ServiceModule, 60,
                "4.1 m diameter  ·  8.6 t propellant",
                "Built by ESA and Airbus, this is Orion's engine room. It carries the " +
                "propulsion, propellant, electrical power, water, oxygen and thermal " +
                "radiators, and supplies all of it to the crew module through an umbilical.",
                "It is jettisoned before re-entry and burns up. Keeping everything that does " +
                "not need to survive 2,760\u00A0°C outside the part that does means the heat " +
                "shield protects 8.5 tonnes instead of the full 26-tonne stack.");

            Add(map, "solar_arrays", "Solar Array Wings", "Solar Arrays",
                PartZone.ServiceModule, 70,
                "Four wings  ·  19 m span  ·  11.2 kW",
                "Four wings of gallium-arsenide cells spanning 19 metres deployed — enough " +
                "power for two family homes. Each pivots on two axes to track the Sun as the " +
                "spacecraft manoeuvres.",
                "The wings must rotate clear before the main engine fires. The exhaust plume " +
                "is hot, fast and erosive, and would damage any panel left in its path.");

            Add(map, "main_engine", "Orbital Manoeuvring System Engine", "Main Engine",
                PartZone.ServiceModule, 80,
                "AJ10-190  ·  25.7 kN thrust  ·  Isp 316 s",
                "The main engine, used for major trajectory changes including lunar orbit " +
                "insertion and the Trans-Earth Injection burn home. The Artemis I engine had " +
                "previously flown on nineteen Space Shuttle missions.",
                "It burns hypergolic propellants — monomethylhydrazine and nitrogen " +
                "tetroxide — which ignite the instant they touch. There is no ignition " +
                "system, because there is no ignition system to fail. Reliability beats " +
                "performance when the engine must relight days from any help.");

            // ---- Interior -------------------------------------------------
            Add(map, "pressure_vessel", "Crew Module Pressure Vessel", "Pressure Vessel",
                PartZone.Interior, 90,
                "8.95 m³ habitable  ·  Al-Li 2219",
                "The sealed aluminium-lithium structure the crew live inside. It is machined " +
                "and friction-stir-welded from seven large pieces.\n\n" +
                "Total pressurised volume is 19.56 m³, of which 8.95 m³ is habitable — " +
                "about the space of two large telephone boxes, for four people for up to " +
                "21 days.",
                "Friction stir welding stirs the two pieces together while still solid, so the " +
                "metal never melts. Fewer weld defects means less structural margin has to be " +
                "carried for uncertainty, which means less mass.");

            Add(map, "crew_seats", "Crew Seats and Restraints", "Crew Seats",
                PartZone.Interior, 100,
                "Four crew  ·  shock-attenuating pallet",
                "Four custom-fitted seats on a shock-attenuating pallet. They are the crew's " +
                "primary protection during launch and splashdown, not just furniture.",
                "The seats are angled so acceleration is taken through the chest and back " +
                "rather than along the spine — the direction the human body tolerates worst. " +
                "The struts are tuned to each astronaut's mass, because a heavier crew member " +
                "needs a different energy-absorbing profile to see the same deceleration.");

            Add(map, "display_control_panel", "Display and Control Panel", "Control Panel",
                PartZone.Interior, 110,
                "Three reconfigurable displays",
                "Orion's glass cockpit. Apollo carried over 500 switches and dials; Orion " +
                "replaces almost all of it with three reconfigurable screens, borrowing " +
                "heavily from the Boeing 787 flight deck.",
                "Fewer physical switches means less mass and fewer failure points. The " +
                "switches that remain are the ones that must work when the software does " +
                "not: abort, and core life support.");

            Add(map, "hand_controllers", "Rotational and Translational Hand Controllers",
                "Hand Controllers",
                PartZone.Interior, 120,
                "Manual attitude and translation control",
                "Two controllers. The rotational one commands attitude — pitch, yaw and " +
                "roll. The translational one commands movement along three axes, which is " +
                "what you use for the final metres of a docking.",
                "Orbital motion is counter-intuitive: to catch a target ahead of you, you " +
                "thrust backwards, dropping into a lower and faster orbit. Docking is flown " +
                "on relative motion, not on instinct.");

            Add(map, "eclss", "Environmental Control and Life Support", "Life Support",
                PartZone.Interior, 130,
                "Regenerable amine swing bed",
                "The system that keeps the cabin breathable: it scrubs carbon dioxide, " +
                "controls humidity and temperature, and maintains pressure.\n\n" +
                "Two amine beds alternate — one absorbs CO₂ and water vapour from the " +
                "cabin while the other vents to space to release what it captured, then they " +
                "swap.",
                "Nothing is consumed, unlike Apollo's lithium hydroxide canisters. That is " +
                "what makes a 21-day mission possible in 8.95 m³: there is simply no room " +
                "to carry three weeks of expendable scrubbers.");

            Add(map, "avionics", "Avionics and Flight Computers", "Avionics",
                PartZone.Interior, 140,
                "Redundant radiation-tolerant computers",
                "Orion's flight computers, navigation sensors and data networks. Beyond " +
                "Earth's magnetic field there is no shielding from cosmic rays, and a single " +
                "high-energy particle can flip a bit in memory — a single event upset.",
                "The answer is redundancy and voting: several computers run the same " +
                "calculation and compare answers, so a corrupted result is outvoted rather " +
                "than acted on. The processors are deliberately generations behind consumer " +
                "hardware, because larger transistors are far harder for a cosmic ray to " +
                "disturb.");

            Add(map, "stowage", "Stowage and Consumables", "Stowage",
                PartZone.Interior, 150,
                "Food, water, equipment, waste",
                "Lockers holding everything the crew consumes or needs to hand: food, water, " +
                "clothing, medical kit, tools, and stowage for waste on the way home. In " +
                "microgravity nothing stays where it is put, so every item is strapped, " +
                "clipped or netted.",
                "Stowage is also a mass-balance problem. Orion steers during re-entry by " +
                "rolling, using a deliberately offset centre of gravity to generate lift — " +
                "so where the cargo sits genuinely affects where the capsule lands.");

            Add(map, "waste_management", "Waste Management System", "Waste System",
                PartZone.Interior, 160,
                "Compact universal waste system",
                "The spacecraft toilet. Apollo crews used bags; Orion carries a proper " +
                "compact commode in a cabin the size of a small van.",
                "Without gravity, waste must be moved by airflow rather than falling. A fan " +
                "pulls air through a filtered separator, and the system is held slightly " +
                "below cabin pressure so air always flows inwards, never out.");

            Add(map, "docking_hatch", "Forward Docking Hatch", "Docking Hatch",
                PartZone.Interior, 170,
                "Overhead crew transfer tunnel",
                "The hatch above the cabin, leading to the docking adapter. This is the route " +
                "the crew take to transfer into Gateway or a lunar lander once docked.",
                "Before it can be opened, the crew pressurise the tunnel between the two " +
                "hatches and watch it hold, confirming a good seal before committing. In " +
                "microgravity an overhead hatch is no harder to use than a side one — " +
                "orientation inside a spacecraft is a matter of convention, not gravity.");

            AssetDatabase.SaveAssets();
            Debug.Log("[ContentBuilder] Authored " + map.Count + " component assets.");
            return map;
        }

        static void Add(Dictionary<string, PartInfo> map, string id, string name, string shortName,
                        PartZone zone, int order, string spec, string summary, string why)
        {
            string path = PartFolder + "/Part_" + id + ".asset";

            var info = AssetDatabase.LoadAssetAtPath<PartInfo>(path);
            if (info == null)
            {
                info = ScriptableObject.CreateInstance<PartInfo>();
                AssetPaths.CreateAsset(info, path);
            }

            info.partId = id;
            info.displayName = name;
            info.shortName = shortName;
            info.zone = zone;
            info.order = order;
            info.keySpec = spec;
            info.summary = summary;
            info.whyItMatters = why;

            EditorUtility.SetDirty(info);
            map[id] = info;
        }

        // ================================================================
        // Quiz
        // ================================================================

        public static QuizData BuildQuiz()
        {
            AssetPaths.EnsureFolder(DataFolder);
            string path = DataFolder + "/MissionReadinessCheck.asset";

            var quiz = AssetDatabase.LoadAssetAtPath<QuizData>(path);
            if (quiz == null)
            {
                quiz = ScriptableObject.CreateInstance<QuizData>();
                AssetPaths.CreateAsset(quiz, path);
            }

            quiz.title = "MISSION READINESS CHECK";
            quiz.intro = "Ten questions on the spacecraft and the physics of spaceflight. " +
                         "Score 80% or better and you are cleared to fly the burn home.";
            quiz.passFraction = 0.8f;
            quiz.shuffleQuestions = true;
            quiz.shuffleOptions = true;

            quiz.questions = new List<QuizQuestion>
            {
                Q("PROPULSION",
                  "A rocket engine fires in the vacuum of space, with nothing to push against. Why does the rocket still accelerate?",
                  new[]
                  {
                      "The engine throws mass out of the nozzle, and the reaction pushes the rocket the other way",
                      "The exhaust gas pushes against the surrounding air",
                      "Burning fuel reduces the rocket's weight, so it rises",
                      "The nozzle creates a vacuum ahead of the rocket that pulls it forward"
                  }, 0,
                  "Newton's third law. The rocket pushes propellant backwards and the propellant pushes the " +
                  "rocket forwards. Air is irrelevant — rockets work better in vacuum, with no drag.",
                  "main_engine"),

                Q("MASS FRACTION",
                  "Mass fraction is defined as the mass of propellant divided by the total mass of the rocket. What is the mass fraction of an ideal rocket?",
                  new[]
                  {
                      "About 0.91",
                      "Exactly 1.0",
                      "About 0.50",
                      "About 0.25"
                  }, 0,
                  "Roughly 91% propellant, 3% structure and engines, 6% payload. A mass fraction of 1.0 would " +
                  "be nothing but propellant with no tank, structure or payload at all.",
                  "service_module"),

                Q("STAGING",
                  "Why does staging — dropping empty sections during flight — let a rocket reach far higher speeds?",
                  new[]
                  {
                      "The remaining engines no longer have to accelerate empty tanks and structure",
                      "Each stage adds its speed to the one before, multiplying the total",
                      "Dropping mass lets the rocket fall faster toward its target",
                      "Separation shocks give the upper stage an extra push"
                  }, 0,
                  "Once a tank is empty it is dead mass. Discarding it means every remaining kilogram of " +
                  "propellant accelerates a lighter vehicle, which is why staging made reaching the Moon possible.",
                  "service_module"),

                Q("PROPULSION",
                  "Specific impulse is quoted in seconds. What does it actually measure?",
                  new[]
                  {
                      "How much thrust an engine produces per unit weight of propellant consumed per second",
                      "How many seconds the engine can burn before running out of propellant",
                      "How long the engine takes to reach full thrust after ignition",
                      "The number of seconds of thrust needed to reach orbit"
                  }, 0,
                  "Specific impulse is efficiency, not duration. A higher figure means more velocity change " +
                  "from the same mass of propellant. Orion's main engine delivers about 316 seconds in vacuum.",
                  "main_engine"),

                Q("LAUNCH",
                  "What condition must be met at the instant of liftoff?",
                  new[]
                  {
                      "Thrust must exceed the total weight of the vehicle",
                      "Thrust must equal the total weight of the vehicle",
                      "The vehicle must already be moving upward",
                      "Mass fraction must be greater than 0.95"
                  }, 0,
                  "If thrust only equals weight the rocket hovers; it has to exceed weight to accelerate off " +
                  "the pad. This is why a heavy vehicle with unnecessary mass never leaves the ground.",
                  "main_engine"),

                Q("STABILITY",
                  "A rocket is stable in flight when the centre of pressure sits where, relative to the centre of mass?",
                  new[]
                  {
                      "Behind the centre of mass, toward the tail",
                      "Ahead of the centre of mass, toward the nose",
                      "Exactly on the centre of mass",
                      "Position makes no difference to stability"
                  }, 0,
                  "With the centre of pressure behind the centre of mass, aerodynamic forces push the rocket " +
                  "back onto its heading. Ahead of it, any disturbance is amplified and the vehicle tumbles. " +
                  "This is why fins sit at the tail.",
                  "back_shell"),

                Q("ORBITS",
                  "What does escape velocity mean for a spacecraft leaving the Moon?",
                  new[]
                  {
                      "The speed at which it can coast away without ever falling back",
                      "The speed needed to enter a circular orbit just above the surface",
                      "The maximum safe speed for leaving the lunar surface",
                      "The speed of the spacecraft relative to the Earth"
                  }, 0,
                  "Escape velocity is the speed at which kinetic energy balances gravitational binding energy. " +
                  "At 120 km above the Moon it is about 2,300 m/s, against 1,625 m/s for a circular orbit.",
                  "main_engine"),

                Q("MISSION DESIGN",
                  "In the Artemis architecture, where is Orion while astronauts walk on the lunar surface?",
                  new[]
                  {
                      "In lunar orbit — Orion never lands, a separate lander carries the crew down",
                      "Landed beside the crew, waiting to lift off again",
                      "Already on its way back to Earth, returning later to collect them",
                      "Docked to a station on the lunar surface"
                  }, 0,
                  "Orion is a deep-space transport, not a lander. It has no landing legs and no descent engine. " +
                  "A dedicated lander takes the crew to the surface and back to Orion in lunar orbit.",
                  "service_module"),

                Q("RE-ENTRY",
                  "Orion's heat shield reaches about 2,760\u00A0°C on return from the Moon. How does it protect the crew?",
                  new[]
                  {
                      "The material chars and erodes away, carrying the heat off with it",
                      "A coolant loop circulates water behind the shield to absorb the heat",
                      "The shield reflects the heat back into the atmosphere",
                      "The shield conducts the heat into the structure, which spreads it out"
                  }, 0,
                  "It is ablative: the material is sacrificial. It chars and sheds, and the departing material " +
                  "takes the heat energy with it rather than conducting it into the cabin.",
                  "heat_shield"),

                Q("LIFE SUPPORT",
                  "The carbon dioxide scrubber fails on the way home. Why is this an emergency within hours rather than days?",
                  new[]
                  {
                      "Exhaled CO₂ builds up and impairs the crew long before the oxygen runs out",
                      "Carbon dioxide is flammable and risks a cabin fire",
                      "The scrubber also generates all of the cabin's oxygen",
                      "Rising CO₂ causes cabin pressure to fall rapidly"
                  }, 0,
                  "There is plenty of oxygen left. It is the CO₂ the crew exhale, with nowhere to go, that " +
                  "degrades judgement above about 1% and incapacitates at a few percent.",
                  "eclss"),
            };

            EditorUtility.SetDirty(quiz);
            AssetDatabase.SaveAssets();
            Debug.Log("[ContentBuilder] Authored " + quiz.questions.Count +
                      " quiz questions, pass mark " + quiz.PassMark + ".");
            return quiz;
        }

        static QuizQuestion Q(string topic, string prompt, string[] options, int correct,
                              string explanation, string relatedPart)
        {
            return new QuizQuestion
            {
                topic = topic,
                prompt = prompt,
                options = options,
                correctIndex = correct,
                explanation = explanation,
                relatedPartId = relatedPart,
            };
        }
    }
}
