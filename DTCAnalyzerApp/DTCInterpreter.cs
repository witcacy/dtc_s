using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace DTCAnalyzerApp
{
    public class DTCInterpreter
    {
        // Diccionario con subcódigos extendidos del Byte 4 y su significado
        private static readonly Dictionary<string, string> subcodeDict = new Dictionary<string, string>
        {
            { "00", "No Failure Details" }, { "01", "Short to Battery Voltage" },
            { "02", "Short to Ground" }, { "03", "Low Voltage" },
            { "04", "Open Circuit" }, { "05", "Short Circuit Between Wires" },
            { "1F", "Configuration Error" }
        };

        public static string ProcesarTRC(string trcPath)
        {
            // Leer todas las líneas del archivo .trc
            string[] lines = File.ReadAllLines(trcPath);
            var canLines = new List<(string line, string id, string[] data)>();

            // Expresión regular para extraer el CAN ID y los 8 bytes de datos
            Regex regex = new Regex(@"^\s*\d+\)\s+[\d\.]+\s+\d+\s+Rx\s+([0-9A-F]+)\s+-\s+\d+\s+((?:[0-9A-F]{2}\s+)+)");

            // Analizar cada línea que coincida con la estructura CAN
            foreach (string line in lines)
            {
                Match match = regex.Match(line);
                if (match.Success)
                {
                    string id = match.Groups[1].Value;
                    string[] data = match.Groups[2].Value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    canLines.Add((line.Trim(), id, data));
                }
            }

            // Preparar el HTML
            var html = new List<string>();
            html.Add("<html><head><meta charset='UTF-8'><title>Reporte DTC Interpretado</title></head><body>");
            html.Add("<h1>Reporte Visual de Códigos DTC</h1>");
            html.Add("<table border='1' cellspacing='0' cellpadding='6'>");
            html.Add("<tr><th>Línea TRC</th><th>CAN ID</th><th>Bytes</th><th>Desglose Interpretado</th><th>OBD-II</th><th>Subcódigo</th><th>Significado</th><th>Enlace</th></tr>");

            // Usar HashSet para evitar códigos DTC duplicados
            HashSet<string> dtcSet = new HashSet<string>();

            // Procesar cada línea CAN
            foreach (var (linea, id, data) in canLines)
            {
                if (data.Length >= 4)
                {
                    // Extraer primeros 4 bytes relevantes para DTC
                    string b1 = data[0], b2 = data[1], b3 = data[2], b4 = data[3];

                    // Convertir Byte 1 a binario y extraer prefijos
                    int val_b1 = Convert.ToInt32(b1, 16);
                    string bin_b1 = Convert.ToString(val_b1, 2).PadLeft(8, '0');

                    // Bits 7-6 para identificar tipo de código (P, C, B, U)
                    string prefixBits = bin_b1.Substring(0, 2);

                    // Bits 5-0 para obtener parte numérica base del DTC
                    int val_low6 = val_b1 & 0x3F;
                    string mid = val_low6.ToString("X2");

                    // Traducir los bits a letras de categoría OBD-II
                    string prefix = prefixBits switch
                    {
                        "00" => "P", // Powertrain
                        "01" => "C", // Chassis
                        "10" => "B", // Body
                        "11" => "U", // Network
                        _ => "U"
                    };

                    // Armar el código OBD completo (sin subcódigo aún)
                    string code = $"{prefix}{mid}{b2}";

                    // Agregar subcódigo extendido (Byte 4)
                    string fullCode = $"{code}-{b4}";

                    // Evitar duplicados
                    if (!dtcSet.Contains(fullCode))
                    {
                        dtcSet.Add(fullCode);

                        // Buscar el significado del subcódigo
                        string meaning = subcodeDict.ContainsKey(b4.ToUpper()) ? subcodeDict[b4.ToUpper()] : "Desconocido";

                        // Enlace externo a dot.report
                        string link = $"<a href='https://dot.report/dtc/{code}' target='_blank'>{code}</a>";

                        // Sección explicativa HTML
                        string explicacion = $@"
<div style='font-family:monospace; background:#f9f9f9; padding:8px; border-left:5px solid #0074D9;'>
<b style='color:darkblue;'>Línea:</b> <span style='color:green;'>{linea}</span><br/>
<b style='color:darkred;'>Byte 1:</b> {b1} → binario: <b>{bin_b1}</b><br/>
├── <b>Bits 7-6</b>: <b>{prefixBits}</b> → Tipo de código OBD-II: <span style='color:blue; font-weight:bold'>{prefix}</span> 
(<i>{(prefix == "P" ? "Powertrain" : prefix == "C" ? "Chassis" : prefix == "B" ? "Body" : "Network")}</i>)<br/>
└── <b>Bits 5-0</b>: <b>{Convert.ToString(val_low6, 2).PadLeft(6, '0')}</b> → decimal: <b>{val_low6}</b> → hex: <b>{mid}</b><br/>
<b style='color:darkred;'>Byte 2:</b> {b2} → concatenado con Byte 1 → <b>Código OBD-II:</b> <span style='color:darkgreen;'>{prefix}{mid}{b2}</span><br/>
<b style='color:darkred;'>Byte 4:</b> {b4} → Subcódigo diagnóstico: <i>{meaning}</i>
</div>";

                        // Agregar fila al HTML
                        html.Add($"<tr><td>{linea}</td><td>{id}</td><td>{b1} {b2} {b3} {b4}</td><td>{explicacion}</td><td>{code}</td><td>{b4}</td><td>{meaning}</td><td>{link}</td></tr>");
                    }
                }
            }

            html.Add("</table></body></html>");

            // Guardar archivo HTML final
            string outPath = Path.Combine(Path.GetDirectoryName(trcPath), "Reporte_DTCs_Interpretado_Visual.html");
            File.WriteAllText(outPath, string.Join("\n", html));
            return outPath;
        }
        public static string GenerarReporteInterpretacionDetallada(string trcPath)
        {
            string[] lines = File.ReadAllLines(trcPath);
            Regex regex = new Regex(@"([0-9A-F]{2}\s){3}[0-9A-F]{2}");

            var html = new List<string>();
            html.Add("<html><head><meta charset='UTF-8'><title>DTC Interpretado</title></head><body>");
            html.Add("<h1>Interpretación Técnica de DTC desde Bytes CAN</h1>");

            var subcodeDict = new Dictionary<string, string>
    {
        { "00", "No Failure Details" }, { "01", "Short to Battery Voltage" },
        { "02", "Short to Ground" }, { "03", "Low Voltage" },
        { "04", "Open Circuit" }, { "05", "Short Circuit Between Wires" },
        { "1F", "Configuration Error" }
    };

            HashSet<string> codigosProcesados = new HashSet<string>();

            foreach (string line in lines)
            {
                Match match = regex.Match(line);
                if (!match.Success) continue;

                string[] bytes = match.Value.Trim().Split(' ');
                if (bytes.Length < 4) continue;

                string b1 = bytes[0], b2 = bytes[1], b3 = bytes[2], b4 = bytes[3];
                int val_b1 = Convert.ToInt32(b1, 16);
                string bin_b1 = Convert.ToString(val_b1, 2).PadLeft(8, '0');

                string prefixBits = bin_b1.Substring(0, 2);
                string prefix = prefixBits switch
                {
                    "00" => "P",
                    "01" => "C",
                    "10" => "B",
                    "11" => "U",
                    _ => "U"
                };

                int val_low6 = val_b1 & 0x3F;
                string mid = val_low6.ToString("X2");
                string code = $"{prefix}{mid}{b2}";
                string full = $"{code}-{b4}";

                if (!codigosProcesados.Add(full))
                    continue;

                string subMeaning = subcodeDict.ContainsKey(b4) ? subcodeDict[b4] : "Desconocido";

                html.Add($@"
        <div style='font-family:monospace;background:#f9f9f9;padding:10px;border-left:5px solid #0074D9;margin-bottom:10px'>
        <b style='color:#0074D9'>CAN Bytes:</b> {b1} {b2} {b3} {b4}<br/>
        <b style='color:darkred'>Byte 1:</b> {b1} = bin: <b>{bin_b1}</b><br/>
        ├── Bits 7-6 = <b>{prefixBits}</b> → <b>Tipo:</b> {prefix}<br/>
        └── Bits 5-0 = <b>{val_low6}</b> → Hex: <b>{mid}</b><br/>
        <b>Byte 2:</b> {b2} → Concat: <b>{code}</b><br/>
        <b>Byte 4:</b> {b4} = Subcódigo → <b>{subMeaning}</b><br/>
        <b>Interpretación:</b> <span style='color:green'>{full}</span><br/>
        <b>Link:</b> <a href='https://dot.report/dtc/{code}' target='_blank'>{code}</a>
        </div>");
            }

            html.Add("</body></html>");
            string output = Path.Combine(Path.GetDirectoryName(trcPath), "Reporte_DTC_CAN_Uds.html");
            File.WriteAllText(output, string.Join("\n", html));
            return output;
        }

        public static string GenerarReporteCompleto(string trcPath)
        {
            string[] lines = File.ReadAllLines(trcPath);
            Regex regex = new Regex(@"^\s*\d+\)\s+[\d\.]+\s+\d+\s+Rx\s+([0-9A-F]+)\s+-\s+\d+\s+((?:[0-9A-F]{2}\s+)+)");

            var html = new List<string>();
            html.Add("<html><head><meta charset='UTF-8'><title>Reporte Completo DTC</title>" +
                      "<style>table{border-collapse:collapse;font-size:12px}th,td{border:1px solid #999;padding:4px}th{background:#eee}</style></head><body>");
            html.Add("<h1>Reporte Completo de DTCs</h1>");
            html.Add("<table><tr><th>CAN ID</th><th>DTC Hex</th><th>Código OBD-II</th><th>Subcódigo</th><th>Significado</th><th>Link</th></tr>");

            HashSet<string> dtcSet = new HashSet<string>();

            foreach (string line in lines)
            {
                Match m = regex.Match(line);
                if (!m.Success) continue;

                string id = m.Groups[1].Value;
                string[] data = m.Groups[2].Value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

                for (int i = 0; i <= data.Length - 4; i++)
                {
                    string b1 = data[i];
                    string b2 = data[i + 1];
                    string b3 = data[i + 2];
                    string b4 = data[i + 3];

                    int valB1 = Convert.ToInt32(b1, 16);
                    string prefixBits = Convert.ToString(valB1, 2).PadLeft(8, '0').Substring(0, 2);
                    string prefix = prefixBits switch
                    {
                        "00" => "P",
                        "01" => "C",
                        "10" => "B",
                        "11" => "U",
                        _ => "U"
                    };

                    int valLow6 = valB1 & 0x3F;
                    string mid = valLow6.ToString("X2");
                    string code = $"{prefix}{mid}{b2}";
                    string fullCode = $"{code}-{b4}";

                    if (!dtcSet.Add(id + fullCode))
                        continue;

                    string meaning = subcodeDict.ContainsKey(b4.ToUpper()) ? subcodeDict[b4.ToUpper()] : "Desconocido";
                    string link = $"<a href='https://dot.report/dtc/{code}' target='_blank'>{code}</a>";

                    html.Add($"<tr><td>{id}</td><td>{b1} {b2} {b3} {b4}</td><td>{code}</td><td>{b4}</td><td>{meaning}</td><td>{link}</td></tr>");
                }
            }

            html.Add("</table></body></html>");
            string outFile = Path.Combine(Path.GetDirectoryName(trcPath), "Reporte_DTC_Completo.html");
            File.WriteAllText(outFile, string.Join("\n", html));
            return outFile;
        }

        public static string LecturaUDSPeriodica(string trcPath)
        {
            string[] lines = File.ReadAllLines(trcPath);
            Regex regex = new Regex(@"^\s*\d+\)\s+([\d\.]+)\s+\d+\s+Rx\s+([0-9A-F]+)\s+-\s+\d+\s+((?:[0-9A-F]{2}\s+)+)");

            var html = new List<string>();
            html.Add("<html><head><meta charset='UTF-8'><title>Lectura UDS Peri\u00F3dica</title>" +
                      "<style>table{border-collapse:collapse;font-size:12px}th,td{border:1px solid #999;padding:4px}th{background:#eee}</style></head><body>");
            html.Add("<h1>Mensajes UDS 0x2A - Lectura Peri\u00F3dica</h1>");
            html.Add("<table><tr><th>Tiempo (ms)</th><th>CAN ID</th><th>DID</th><th>Datos</th></tr>");

            foreach (string line in lines)
            {
                Match m = regex.Match(line);
                if (!m.Success) continue;

                string time = m.Groups[1].Value;
                string id = m.Groups[2].Value;
                string[] data = m.Groups[3].Value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

                if (data.Length < 3) continue;
                if (!data[0].Equals("2A", StringComparison.OrdinalIgnoreCase))
                    continue;

                string did = data[1] + data[2];
                string datos = string.Join(" ", data, 3, data.Length - 3);

                html.Add($"<tr><td>{time}</td><td>{id}</td><td>{did}</td><td>{datos}</td></tr>");
            }

            html.Add("</table></body></html>");
            string outFile = Path.Combine(Path.GetDirectoryName(trcPath), "Reporte_UDS_2A_Periodico.html");
            File.WriteAllText(outFile, string.Join("\n", html));
            return outFile;
        }

    }

}
