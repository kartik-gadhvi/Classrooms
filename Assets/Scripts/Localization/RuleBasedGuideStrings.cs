using System.Collections.Generic;
using System.Text;

/// <summary>
/// Fixed question topics and answers of the rule-based guide. Answers are written to be
/// spoken (numbers in words) and are pre-recorded with the tutor voice.
/// </summary>
public static class RuleBasedGuideStrings
{
    public const string NoMatchId = "nomatch";
    public const string HelpId = "help";
    private const string HintPrefix = "hint_";
    private const float HelpKeywordWeight = 0.75f;
    private const float FocusBonus = 0.5f;

    public struct Line
    {
        public string id;
        public string de;
        public string en;
    }

    private class Topic
    {
        public string id;
        public string de;
        public string en;
        public string[] keywords;
        public int minStep;
        public int maxStep = int.MaxValue;
        public int revealStep;
        public int[] focus;
    }

    private static readonly Topic[] Topics =
    {
        new Topic
        {
            id = "atomizer",
            keywords = new[] { "zerstaeuber*", "zerstaeub*", "atomizer*", "atomiser*", "spray*", "sprueh*", "pump*", "gummiball", "bulb", "erzeug*", "create*", "produce*", "make droplets", "make some droplets" },
            focus = new[] { 11, 11, 17, 17, 33, 33 },
            de = "Der Zerstäuber erzeugt viele winzige Öltröpfchen. Beim Zerstäuben werden einige davon durch Reibung elektrisch geladen. Ziele mit dem rechten Controller auf den Zerstäuber und drücke den rechten Trigger.",
            en = "The atomizer creates many tiny oil droplets. As they are sprayed, friction gives some of them an electric charge. Aim the right controller at the atomizer and press the right trigger."
        },
        new Topic
        {
            id = "microscope",
            keywords = new[] { "mikroskop*", "microscope*", "gespiegelt*", "spiegel*", "flipped", "flip*", "upside down", "auf dem kopf" },
            focus = new[] { 12, 13 },
            de = "Das Mikroskop macht die winzigen Tröpfchen sichtbar. Es spiegelt aber das Bild, deshalb scheinen fallende Tröpfchen im echten Versuch zu steigen. In dieser Simulation siehst du die Bewegung direkt.",
            en = "The microscope makes the tiny droplets visible. But it flips the image, so in the real experiment falling droplets seem to rise. In this simulation you see the motion directly."
        },
        new Topic
        {
            id = "light",
            keywords = new[] { "licht*", "lampe*", "light*", "lamp*", "beleucht*", "leucht*", "glow*", "shine*", "helle", "hellen", "bright*" },
            focus = new[] { 14, 14 },
            de = "Das Licht kommt von der Seite. Die Öltröpfchen streuen dieses Licht und leuchten deshalb als kleine helle Punkte auf.",
            en = "The light comes in from the side. The oil droplets scatter it, so they show up as small bright points."
        },
        new Topic
        {
            id = "plates",
            keywords = new[] { "platte*", "plattenabstand", "kondensator*", "capacitor*", "plate*", "abstand", "distance", "millimeter*", "kammer*", "chamber*" },
            focus = new[] { 15, 15, 29, 32 },
            de = "Das Herzstück sind zwei Metallplatten, die einen Kondensator bilden. Sie sind sechs Millimeter voneinander entfernt. Liegt eine Spannung an, entsteht zwischen ihnen ein elektrisches Feld.",
            en = "The heart of the apparatus is two metal plates that form a capacitor. They are six millimeters apart. When a voltage is applied, an electric field forms between them."
        },
        new Topic
        {
            id = "voltage_knob",
            keywords = new[] { "spannungsregler*", "regler*", "knob*", "dial*", "dreh*", "turn*", "greif*", "grab*", "plus", "minus", "feineinstell*", "fine tun*", "slider" },
            focus = new[] { 16, 16, 33, 40 },
            de = "Greife den Spannungsregler mit dem linken Controller und bewege die Hand langsam nach links oder rechts. Für kleine Änderungen benutzt du den Slider oder die Plus- und Minus-Tasten mit dem linken Trigger.",
            en = "Grab the voltage knob with the left controller and move your hand slowly to the left or right. For small changes, use the slider or the plus and minus buttons with the left trigger."
        },
        new Topic
        {
            id = "voltage",
            keywords = new[] { "spannung*", "voltage*", "volt", "volts", "elektrisches feld", "electric field", "feld*", "field*", "feldstaerke*", "field strength" },
            focus = new[] { 29, 32 },
            de = "Die Spannung erzeugt das elektrische Feld zwischen den Platten. Die Feldstärke ist die Spannung geteilt durch den Plattenabstand. Je höher die Spannung, desto stärker zieht das Feld an einem geladenen Tröpfchen.",
            en = "The voltage creates the electric field between the plates. The field strength is the voltage divided by the distance between the plates. The higher the voltage, the more strongly the field pulls on a charged droplet."
        },
        new Topic
        {
            id = "hover_concept",
            keywords = new[] { "schweb*", "hover*", "float*", "gleichgewicht*", "kraeftegleichgewicht*", "balance*", "warum schwebt", "why does it hover", "why does it float", "why is it hovering" },
            focus = new[] { 32, 32, 36, 38 },
            de = "Ein Tröpfchen schwebt, wenn die elektrische Kraft nach oben genauso groß ist wie die Gewichtskraft nach unten. Dann heben sich die beiden Kräfte auf, und das Tröpfchen bleibt in der Schwebe.",
            en = "A droplet hovers when the upward electric force is exactly as large as the downward gravitational force. The two forces cancel out, and the droplet stays in place."
        },
        new Topic
        {
            id = "hover_task",
            keywords = new[] { "schweb*", "hover*", "float*", "still", "stillstehen", "stehen bleib*", "stop*", "anhalten" },
            minStep = 33,
            maxStep = 40,
            focus = new[] { 35, 35 },
            de = "Erhöhe die Spannung langsam, bis das Tröpfchen nicht mehr fällt. Steigt es, war es zu viel, dann nimm die Spannung ein wenig zurück. Ziel ist, dass es weder deutlich steigt noch fällt.",
            en = "Raise the voltage slowly until the droplet stops falling. If it starts to rise, that was too much, so turn the voltage down a little. The goal is that it neither clearly rises nor falls."
        },
        new Topic
        {
            id = "rising_falling",
            keywords = new[] { "steig*", "faellt", "fallen", "falls", "falling", "rise*", "rising", "nach oben", "nach unten", "going up", "going down", "moves up", "zu schnell", "too fast" },
            minStep = 33,
            maxStep = 40,
            focus = new[] { 33, 40 },
            de = "Steigt das Tröpfchen, ist die elektrische Kraft größer als die Gewichtskraft. Verringere dann die Spannung. Fällt es, ist die elektrische Kraft zu klein, dann erhöhe die Spannung. Ändere sie am besten in kleinen Schritten.",
            en = "If the droplet rises, the electric force is larger than its weight, so lower the voltage. If it falls, the electric force is too small, so raise the voltage. It works best in small steps."
        },
        new Topic
        {
            id = "select_drop",
            keywords = new[] { "auswaehl*", "waehl*", "select*", "choose", "pick*", "roter strahl", "red beam", "strahl*", "beam*", "markier*", "anklick*", "click*" },
            focus = new[] { 34, 34 },
            de = "Ziele mit dem roten Strahl des rechten Controllers auf ein Tröpfchen und drücke den rechten Trigger. Das gewählte Tröpfchen wird markiert, und seine Werte erscheinen auf dem Panel.",
            en = "Aim the red beam of the right controller at a droplet and press the right trigger. The chosen droplet is highlighted, and its values appear on the panel."
        },
        new Topic
        {
            id = "radius_slider",
            keywords = new[] { "slider", "schieberegler*", "schieb*", "radius einstell*", "set the radius", "adjust the radius", "einstell*", "stell*", "set*", "adjust*", "change*", "aender*", "veraender*", "beweg*", "move*" },
            minStep = 23,
            maxStep = 28,
            focus = new[] { 23, 28 },
            de = "Halte den Radius-Slider mit dem linken Trigger fest und bewege ihn. Links werden die Tröpfchen kleiner, rechts größer. Erzeuge danach mit dem rechten Trigger am Zerstäuber neue Tröpfchen.",
            en = "Hold the radius slider with the left trigger and move it. To the left the droplets get smaller, to the right larger. Then create new droplets with the right trigger at the atomizer."
        },
        new Topic
        {
            id = "radius",
            keywords = new[] { "radius", "radien", "groesse*", "size*", "gross", "grosse", "klein*", "big*", "small*", "large*", "durchmesser", "diameter" },
            focus = new[] { 19, 28 },
            de = "Der Radius bestimmt, wie schnell ein Tröpfchen fällt. Größere Tröpfchen sind schwerer und fallen schneller, kleinere fallen langsamer. Deshalb kann man den Radius aus der Fallgeschwindigkeit bestimmen.",
            en = "The radius determines how fast a droplet falls. Larger droplets are heavier and fall faster, smaller ones fall more slowly. That is why the radius can be found from the falling speed."
        },
        new Topic
        {
            id = "gravity",
            keywords = new[] { "schwerkraft*", "gravitation*", "gravity", "gewichtskraft*", "gewicht*", "weight*", "masse*", "mass", "dichte*", "density", "erdbeschleunigung*", "warum fallen", "why do they fall", "why does it fall", "nach unten", "fall down", "falling down" },
            focus = new[] { 18, 22 },
            de = "Die Schwerkraft zieht jedes Tröpfchen nach unten. Die Gewichtskraft ist Masse mal Erdbeschleunigung. Die Masse ergibt sich aus der Dichte des Öls und dem Volumen des Tröpfchens, also aus seinem Radius.",
            en = "Gravity pulls every droplet downward. The gravitational force is mass times the acceleration due to gravity. The mass follows from the density of the oil and the volume of the droplet, so from its radius."
        },
        new Topic
        {
            id = "charged_how",
            keywords = new[] { "geladen*", "aufgeladen*", "aufladen*", "charged", "get charged", "get a charge", "durch reibung", "by friction", "ionen*", "ions" },
            focus = new[] { 11, 11 },
            de = "Beim Zerstäuben reiben die Tröpfchen an der Düse. Dabei nehmen manche Tröpfchen Elektronen auf oder geben welche ab. So sind sie elektrisch geladen, und das Feld kann auf sie wirken.",
            en = "When the oil is sprayed, the droplets rub against the nozzle. Some of them pick up or lose electrons in the process. That leaves them electrically charged, so the field can act on them."
        },
        new Topic
        {
            id = "stokes",
            keywords = new[] { "stokes", "luftreibung*", "reibung*", "luftwiderstand*", "air resistance", "drag", "friction", "viskositaet*", "viscosity", "konstant*", "constant speed", "gleichmaessig*" },
            focus = new[] { 18, 22 },
            de = "Fällt ein Tröpfchen durch die Luft, bremst es die Luftreibung. Nach dem Gesetz von Stokes hängt diese Reibungskraft vom Radius, von der Geschwindigkeit und von der Viskosität der Luft ab. Deshalb fällt ein Tröpfchen schon nach kurzer Zeit mit konstanter Geschwindigkeit.",
            en = "When a droplet falls through air, air friction slows it down. According to Stokes' law, this drag depends on the radius, the speed, and the viscosity of air. That is why a droplet soon falls at a constant speed."
        },
        new Topic
        {
            id = "electric_force",
            keywords = new[] { "elektrische kraft", "electric force", "coulomb kraft", "coulombkraft", "coulomb force", "gruener pfeil", "green arrow", "pfeil*", "arrow*", "kraft", "force" },
            focus = new[] { 29, 32, 35, 38 },
            de = "Die elektrische Kraft ist die Ladung mal die Feldstärke. Der grüne Pfeil zeigt sie an. Erhöhst du die Spannung, wird das Feld stärker und der grüne Pfeil länger.",
            en = "The electric force is the charge times the field strength. The green arrow shows it. When you raise the voltage, the field gets stronger and the green arrow grows."
        },
        new Topic
        {
            id = "charge_formula",
            keywords = new[] { "ladung berechn*", "berechn*", "calculat*", "formel*", "formula*", "gleichung*", "equation*", "rechn*", "compute*", "wie bestimm*", "how do we get the charge", "q" },
            focus = new[] { 36, 40 },
            de = "Wenn das Tröpfchen schwebt, ist die elektrische Kraft gleich der Gewichtskraft. Daraus folgt: Die Ladung ist Masse mal Erdbeschleunigung mal Plattenabstand, geteilt durch die Spannung.",
            en = "When the droplet hovers, the electric force equals the gravitational force. From that it follows: the charge is mass times the acceleration due to gravity times the plate distance, divided by the voltage."
        },
        new Topic
        {
            id = "elementary_charge",
            keywords = new[] { "elementarladung*", "elementary charge", "elementary", "kleinste ladung", "smallest charge", "wert von e", "value of e", "ladung des elektrons", "charge of an electron", "charge of the electron", "elektronenladung*", "wie gross ist e", "how big is e", "e" },
            focus = new[] { 44, 46, 58, 65 },
            de = "Die Elementarladung e ist die kleinste frei vorkommende elektrische Ladung. Ihr heutiger Wert ist etwa eins Komma sechs null zwei mal zehn hoch minus neunzehn Coulomb. Ein Elektron trägt genau eine negative Elementarladung.",
            en = "The elementary charge e is the smallest electric charge found freely in nature. Today's value is about one point six zero two times ten to the minus nineteen coulombs. An electron carries exactly one negative elementary charge."
        },
        new Topic
        {
            id = "quantization",
            keywords = new[] { "quantis*", "quantiz*", "gequantelt*", "quantel*", "vielfach*", "multiple*", "ganzzahl*", "integer*", "whole number*", "paket*", "packet*", "diskret*", "discrete", "kontinuierlich*", "continuous*", "unteilbar*", "indivisible" },
            focus = new[] { 41, 46, 61, 65 },
            de = "Ladungsquantisierung bedeutet: Jede Ladung ist ein ganzzahliges Vielfaches der Elementarladung. Ein Tröpfchen kann eine, zwei oder drei Elementarladungen tragen, aber nie eine halbe.",
            en = "Charge quantization means that every charge is a whole-number multiple of the elementary charge. A droplet can carry one, two, or three elementary charges, but never half of one."
        },
        new Topic
        {
            id = "histogram",
            keywords = new[] { "histogramm*", "histogram*", "balken*", "bars", "bar", "diagramm*", "diagram*", "chart*", "graph*", "verteilung*", "distribution*", "haeuf*" },
            focus = new[] { 41, 46 },
            de = "Das Histogramm zeigt, wie oft deine gemessenen Ladungen bei bestimmten Werten liegen. Die Balken häufen sich bei ganzzahligen Vielfachen der Elementarladung. Genau das zeigt, dass Ladung gequantelt ist.",
            en = "The histogram shows how often your measured charges fall at particular values. The bars cluster at whole-number multiples of the elementary charge. That is exactly what shows that charge is quantized."
        },
        new Topic
        {
            id = "more_measurements",
            keywords = new[] { "fuenf", "five", "mehrere", "several", "noch eine", "noch ein", "another one", "more droplets", "mehr troepfchen", "wie viele", "how many", "repeat the measurement" },
            focus = new[] { 36, 41 },
            de = "Eine einzelne Messung ist nur ein Datenpunkt. Erst mit mehreren Tröpfchen sieht man, dass alle Ladungen Vielfache derselben kleinsten Einheit sind. Deshalb messen wir fünf Tröpfchen.",
            en = "A single measurement is only one data point. Only with several droplets can you see that all charges are multiples of the same smallest unit. That is why we measure five droplets."
        },
        new Topic
        {
            id = "oil",
            keywords = new[] { "oel", "oil", "wasser", "water", "verdunst*", "evaporat*", "warum oel", "why oil" },
            focus = new[] { 7, 7 },
            de = "Harvey Fletcher schlug vor, Öl statt Wasser zu verwenden. Wassertröpfchen verdunsten schnell und werden dabei immer kleiner. Öltröpfchen bleiben lange stabil, so kann man ein einzelnes Tröpfchen sehr lange beobachten.",
            en = "Harvey Fletcher suggested using oil instead of water. Water droplets evaporate quickly and keep shrinking. Oil droplets stay stable for a long time, so a single droplet can be observed for a long while."
        },
        new Topic
        {
            id = "millikan",
            keywords = new[] { "wer bist du", "wer sind sie", "who are you", "wer ist millikan", "who is millikan", "who was millikan", "ueber dich", "about you", "about yourself", "dein name", "your name", "nobel*", "wo kommst du her", "where are you from", "chicago", "caltech" },
            focus = new[] { 0, 9 },
            de = "Ich bin Robert Andrews Millikan, Physiker aus den Vereinigten Staaten. Ich habe in Chicago und später am Caltech geforscht. Für die Messung der Elementarladung erhielt ich neunzehnhundertdreiundzwanzig den Nobelpreis für Physik.",
            en = "I am Robert Andrews Millikan, a physicist from the United States. I worked in Chicago and later at Caltech. In nineteen twenty-three I received the Nobel Prize in Physics for measuring the elementary charge."
        },
        new Topic
        {
            id = "fletcher",
            keywords = new[] { "fletcher", "harvey", "doktorand*", "mitarbeiter*", "colleague*" },
            focus = new[] { 6, 7 },
            de = "Harvey Fletcher war mein Doktorand. Er hatte die Idee, Öl statt Wasser zu verwenden, und hat viele der Messungen gemeinsam mit mir durchgeführt.",
            en = "Harvey Fletcher was my doctoral student. He had the idea of using oil instead of water, and he carried out many of the measurements together with me."
        },
        new Topic
        {
            id = "thomson",
            keywords = new[] { "thomson", "thompson", "elektron entdeckt", "discovered the electron", "entdeckung des elektrons", "discovery of the electron", "elektron", "elektronen", "electron", "electrons" },
            focus = new[] { 8, 8 },
            de = "J. J. Thomson hatte gezeigt, dass es Elektronen gibt. Offen war aber, wie groß ihre Ladung ist und ob alle Elektronen dieselbe Ladung tragen. Genau das wollte ich mit diesem Versuch messen.",
            en = "J. J. Thomson had shown that electrons exist. What remained open was how large their charge is, and whether all electrons carry the same charge. That is exactly what I wanted to measure with this experiment."
        },
        new Topic
        {
            id = "goal",
            keywords = new[] { "ziel", "das ziel", "goal*", "worum geht", "what is this about", "zweck*", "purpose", "warum machen wir", "why are we doing", "forschungsfrage*", "research question", "was messen wir", "what are we measuring", "what do we measure" },
            focus = new[] { 0, 9 },
            de = "Wir wollen herausfinden, ob elektrische Ladung in kleinsten, unteilbaren Paketen vorkommt, und wie groß dieses kleinste Paket ist. Dazu messen wir die Ladung einzelner Öltröpfchen.",
            en = "We want to find out whether electric charge comes in smallest, indivisible packets, and how large this smallest packet is. To do that, we measure the charge of individual oil droplets."
        },
        new Topic
        {
            id = "notebook",
            keywords = new[] { "notizbuch*", "notebook*", "holton", "franklin", "aussortiert*", "weggelassen*", "verworfen*", "discard*", "excluded", "exclude*", "left out", "betrug*", "fraud*", "gefaelscht*", "faked", "manipul*", "datenauswahl", "datenselektion", "data selection", "kontrovers*", "controvers*", "ehrlich*", "honest*", "cheat*", "geschummelt" },
            revealStep = 47,
            focus = new[] { 47, 57 },
            de = "In meinen Notizbüchern stehen mehr Messungen, als ich veröffentlicht habe. Ich habe Messungen mit technischen Problemen aussortiert. Allan Franklin zeigte später, dass mein Ergebnis auch mit allen Daten fast gleich geblieben wäre. Wichtig ist, offen zu dokumentieren, was man weglässt und warum.",
            en = "My notebooks contain more measurements than I published. I left out measurements with technical problems. Allan Franklin later showed that my result would have stayed almost the same with all the data. What matters is to document openly what you leave out and why."
        },
        new Topic
        {
            id = "final_value",
            keywords = new[] { "endwert*", "final value", "veroeffentlicht*", "published", "1913", "abweichung*", "deviation", "differ*", "unterschied*", "heutige wert", "heutigen wert", "today s value", "todays value", "accura*", "praezis*" },
            revealStep = 58,
            focus = new[] { 58, 66 },
            de = "Neunzehnhundertdreizehn habe ich eins Komma fünf neun zwei mal zehn hoch minus neunzehn Coulomb veröffentlicht. Der heutige Wert ist etwas größer. Die Abweichung kam von einem leicht falschen Wert für die Viskosität der Luft, nicht von der Methode.",
            en = "In nineteen thirteen I published one point five nine two times ten to the minus nineteen coulombs. Today's value is slightly larger. The difference came from a slightly wrong value for the viscosity of air, not from the method."
        },
        new Topic
        {
            id = "controls",
            keywords = new[] { "welche taste", "which button", "welcher knopf", "tasten", "buttons", "steuerung", "controls", "bedienung", "wie bediene" },
            de = "Mit A gehst du weiter, und mit X wiederhole ich meine letzte Erklärung. Halte Y gedrückt, um mir eine Frage zu stellen. Was du für die aktuelle Aufgabe brauchst, steht im Hinweisfeld.",
            en = "Press A to continue, and press X to have me repeat my last explanation. Hold Y to ask me a question. What you need for the current task is shown in the guide panel."
        },
        new Topic
        {
            id = "teleport",
            keywords = new[] { "teleport*", "laufen", "walk*", "gehen", "hingehen", "hinkommen", "get there", "go there", "joystick*", "beweg*", "move", "moving" },
            focus = new[] { 4, 4 },
            de = "Halte den rechten Joystick nach vorne gedrückt und ziele auf eine Stelle am Boden. Wenn du loslässt, teleportierst du dich dorthin.",
            en = "Hold the right joystick forward and aim at a spot on the floor. When you let go, you teleport there."
        },
        new Topic
        {
            id = "greeting",
            keywords = new[] { "hallo", "hello", "hi", "hey", "guten tag", "guten morgen", "good morning", "servus", "moin" },
            de = "Hallo! Ich bin hier, um dir bei meinem Experiment zu helfen. Frag mich ruhig etwas zu den Geräten oder zur Physik dahinter.",
            en = "Hello! I am here to help you with my experiment. Feel free to ask me about the equipment or the physics behind it."
        },
        new Topic
        {
            id = "thanks",
            keywords = new[] { "danke*", "vielen dank", "thank*", "thanks", "alles klar", "verstanden", "got it", "understood" },
            de = "Gern geschehen. Wenn du noch eine Frage hast, halte einfach Y gedrückt.",
            en = "You are welcome. If you have another question, just hold Y."
        },
        new Topic
        {
            id = HelpId,
            keywords = new[] { "hilfe", "help", "helfen", "was soll ich", "was muss ich", "was mache ich", "what should i", "what do i", "what now", "was jetzt", "wie geht es weiter", "next", "naechste*", "weiter", "stuck", "haenge", "komme nicht weiter", "verstehe nicht", "don t understand", "dont understand", "aufgabe*", "task*", "wie geht das", "how does this work", "how do i", "nochmal", "again", "repeat", "wiederhol*" }
        }
    };

    private static readonly Dictionary<ExperimentPhase, Line> Hints = new Dictionary<ExperimentPhase, Line>
    {
        { ExperimentPhase.Introduction, Hint(ExperimentPhase.Introduction,
            "Hör dir in Ruhe an, was ich erzähle. Mit A gehst du weiter, und wenn ich dir eine Frage stelle, antworte mit A für Ja.",
            "Just listen to what I am telling you. Press A to continue, and when I ask you a question, answer with A for yes.") },
        { ExperimentPhase.WalkToApparatus, Hint(ExperimentPhase.WalkToApparatus,
            "Komm zu meinem Experiment. Halte den rechten Joystick nach vorne, ziele auf die Stelle beim grünen Pfeil und lass dann los.",
            "Come over to my experiment. Hold the right joystick forward, aim at the spot by the green arrow, and then let go.") },
        { ExperimentPhase.ApparatusTour, Hint(ExperimentPhase.ApparatusTour,
            "Ich stelle dir gerade meinen Apparat und seine Geschichte vor. Hör zu und drücke A, wenn du weitermachen möchtest. Mit X wiederhole ich die letzte Erklärung.",
            "Right now I am introducing my apparatus and its history. Listen, and press A when you want to continue. Press X and I will repeat my last explanation.") },
        { ExperimentPhase.FirstSpray, Hint(ExperimentPhase.FirstSpray,
            "Deine Aufgabe ist jetzt, Öltröpfchen zu erzeugen. Ziele mit dem rechten Controller auf den Zerstäuber und drücke den rechten Trigger.",
            "Your task now is to create oil droplets. Aim the right controller at the atomizer and press the right trigger.") },
        { ExperimentPhase.GravityAndRadius, Hint(ExperimentPhase.GravityAndRadius,
            "Gerade geht es darum, warum die Tröpfchen fallen. Die Schwerkraft zieht sie nach unten, und aus ihrer Fallgeschwindigkeit bestimmen wir den Radius. Drücke A, um weiterzugehen.",
            "Right now we are looking at why the droplets fall. Gravity pulls them down, and from their falling speed we determine the radius. Press A to continue.") },
        { ExperimentPhase.RadiusSliderIntro, Hint(ExperimentPhase.RadiusSliderIntro,
            "Mit dem Slider stellst du die Größe der Tröpfchen ein. Halte ihn mit dem linken Trigger fest und bewege ihn. Drücke danach A.",
            "The slider sets the size of the droplets. Hold it with the left trigger and move it. Then press A.") },
        { ExperimentPhase.RadiusTasks, Hint(ExperimentPhase.RadiusTasks,
            "Stelle mit dem Slider den Radius ein, der im Hinweisfeld steht. Erzeuge dann mit dem rechten Trigger Tröpfchen und warte, bis eines zwischen die Platten fällt.",
            "Use the slider to set the radius shown in the guide panel. Then create droplets with the right trigger and wait until one falls between the plates.") },
        { ExperimentPhase.RadiusWrapUp, Hint(ExperimentPhase.RadiusWrapUp,
            "Du hast gesehen, dass der Radius die Fallgeschwindigkeit bestimmt. Drücke A, um mit der eigentlichen Messung weiterzumachen.",
            "You have seen that the radius determines the falling speed. Press A to move on to the actual measurement.") },
        { ExperimentPhase.ElectricField, Hint(ExperimentPhase.ElectricField,
            "Jetzt geht es um das elektrische Feld zwischen den Platten. Je höher die Spannung, desto stärker wirkt die elektrische Kraft auf ein geladenes Tröpfchen. Drücke A, um weiterzugehen.",
            "Now we are looking at the electric field between the plates. The higher the voltage, the stronger the electric force on a charged droplet. Press A to continue.") },
        { ExperimentPhase.MeasurementSpray, Hint(ExperimentPhase.MeasurementSpray,
            "Erzeuge ein neues Öltröpfchen. Ziele mit dem rechten Controller auf den Zerstäuber und drücke den rechten Trigger.",
            "Create a new oil droplet. Aim the right controller at the atomizer and press the right trigger.") },
        { ExperimentPhase.SelectDroplet, Hint(ExperimentPhase.SelectDroplet,
            "Wähle jetzt ein Tröpfchen aus. Ziele mit dem roten Strahl des rechten Controllers darauf und drücke den rechten Trigger.",
            "Now select a droplet. Aim the red beam of the right controller at it and press the right trigger.") },
        { ExperimentPhase.HoverDroplet, Hint(ExperimentPhase.HoverDroplet,
            "Bring das ausgewählte Tröpfchen zum Schweben. Greife den Spannungsregler mit dem linken Controller. Steigt das Tröpfchen, verringere die Spannung, fällt es, erhöhe sie.",
            "Make the selected droplet hover. Grab the voltage knob with the left controller. If the droplet rises, lower the voltage; if it falls, raise it.") },
        { ExperimentPhase.MeasurementLoop, Hint(ExperimentPhase.MeasurementLoop,
            "Wenn ein Tröpfchen schwebt, ist die elektrische Kraft gleich der Gewichtskraft, und daraus berechnen wir seine Ladung. Wir brauchen fünf solcher Messungen. Drücke A, um weiterzugehen.",
            "When a droplet hovers, the electric force equals its weight, and from that we calculate its charge. We need five such measurements. Press A to continue.") },
        { ExperimentPhase.Histogram, Hint(ExperimentPhase.Histogram,
            "Schau dir das Histogramm an. Deine Ladungen häufen sich bei ganzzahligen Vielfachen der Elementarladung. Drücke A, um weiterzugehen.",
            "Take a look at the histogram. Your charges cluster at whole-number multiples of the elementary charge. Press A to continue.") },
        { ExperimentPhase.Notebook, Hint(ExperimentPhase.Notebook,
            "Ich zeige dir gerade meine Notizbücher und erzähle, welche Messungen ich verwendet habe. Hör zu und drücke A, um weiterzugehen.",
            "I am showing you my notebooks and telling you which measurements I used. Listen, and press A to continue.") },
        { ExperimentPhase.Closing, Hint(ExperimentPhase.Closing,
            "Wir sind fast fertig. Ich erzähle dir noch, was aus meinem Ergebnis geworden ist. Drücke A, um weiterzugehen.",
            "We are almost done. I am telling you what became of my result. Press A to continue.") }
    };

    private static readonly Line NoMatch = new Line
    {
        id = NoMatchId,
        de = "Dazu kann ich dir leider nichts sagen. Ich kann dir nur bei diesem Experiment helfen.",
        en = "I am afraid I cannot help with that. I can only help you with this experiment."
    };

    /// <summary>Every line that has to exist as a recording.</summary>
    public static IEnumerable<Line> AllLines()
    {
        foreach (Topic topic in Topics)
        {
            if (topic.id != HelpId)
                yield return new Line { id = topic.id, de = topic.de, en = topic.en };
        }

        foreach (Line hint in Hints.Values)
            yield return hint;

        yield return NoMatch;
    }

    /// <summary>Line ids to speak, in order, for a transcribed question at the given tutorial step.</summary>
    public static string[] Reply(string transcript, int step, out string topicId)
    {
        string text = Normalize(transcript);
        Topic best = null;
        float bestScore = 0f;

        foreach (Topic topic in Topics)
        {
            if (step < topic.minStep || step > topic.maxStep)
                continue;

            float score = Score(text, topic);
            if (score <= 0f)
                continue;

            if (InFocus(topic, step))
                score += FocusBonus;

            if (score > bestScore)
            {
                best = topic;
                bestScore = score;
            }
        }

        string hint = HintId(ExperimentPhases.FromStep(step));

        if (best != null && step < best.revealStep)
            best = null;

        if (best == null)
        {
            topicId = NoMatchId;
            return new[] { NoMatchId, hint };
        }

        topicId = best.id;
        return best.id == HelpId ? new[] { hint } : new[] { best.id };
    }

    public static string Text(string id, AppLanguage language)
    {
        foreach (Line line in AllLines())
        {
            if (line.id == id)
                return language == AppLanguage.English ? line.en : line.de;
        }

        return "";
    }

    private static string HintId(ExperimentPhase phase)
    {
        return HintPrefix + phase.ToString().ToLowerInvariant();
    }

    private static Line Hint(ExperimentPhase phase, string de, string en)
    {
        return new Line { id = HintId(phase), de = de, en = en };
    }

    private static float Score(string text, Topic topic)
    {
        float score = 0f;
        foreach (string keyword in topic.keywords)
        {
            if (!Matches(text, keyword))
                continue;

            score += topic.id == HelpId ? HelpKeywordWeight : keyword.Split(' ').Length;
        }

        return score;
    }

    private static bool InFocus(Topic topic, int step)
    {
        if (topic.focus == null)
            return false;

        for (int i = 0; i + 1 < topic.focus.Length; i += 2)
        {
            if (step >= topic.focus[i] && step <= topic.focus[i + 1])
                return true;
        }

        return false;
    }

    /// <summary>Whole-word match; a trailing * matches any word that starts with the keyword.</summary>
    private static bool Matches(string text, string keyword)
    {
        if (keyword.EndsWith("*"))
            return text.Contains(" " + keyword.Substring(0, keyword.Length - 1));

        return text.Contains(" " + keyword + " ");
    }

    private static string Normalize(string s)
    {
        var sb = new StringBuilder((s ?? "").Length + 2).Append(' ');
        bool space = true;

        foreach (char c in (s ?? "").ToLowerInvariant())
        {
            string add;
            switch (c)
            {
                case 'ä': add = "ae"; break;
                case 'ö': add = "oe"; break;
                case 'ü': add = "ue"; break;
                case 'ß': add = "ss"; break;
                default: add = char.IsLetterOrDigit(c) ? c.ToString() : " "; break;
            }

            if (add == " ")
            {
                if (!space)
                    sb.Append(' ');
                space = true;
            }
            else
            {
                sb.Append(add);
                space = false;
            }
        }

        if (!space)
            sb.Append(' ');

        return sb.ToString();
    }
}
