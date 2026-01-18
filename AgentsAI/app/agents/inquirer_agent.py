# app/agents/inquirer_agent.py

import json
from agents import Agent, function_tool
from app.utils.escalamiento_detector import detectar_escalamiento, es_caso_no_escalable, obtener_mensaje_escalamiento
from app.utils.fecha_detector        import detectar_fecha_en_texto, extraer_fecha_aproximada

@function_tool
async def classify_justification_case(query: str) -> str:
    """
    Retorna un JSON con:
      - case: uno de [enfermedad, calamidad, deportiva, viaje_trabajo, clases_virtuales, desconocido, pregunta_informativa]
      - required_doc: documento que debe presentar (o null si no es justificable)
      - follow_up: lista con UNA SOLA pregunta para avanzar el diálogo
      - note: opcional, si debe escalarse a mentor (--mentor--)
      - fecha_detectada / fecha_extraida: metadatos
    """
    q = query.lower()
    tiene_fecha   = detectar_fecha_en_texto(query)
    fecha_extraida = extraer_fecha_aproximada(query) if tiene_fecha else None

    # 0) Detectar preguntas INFORMATIVAS sobre requisitos/documentos
    # Estas deben ir al FAQ, no procesarse como justificación
    palabras_informativas = [
        "qué debe constar", "que debe constar",
        "qué debe tener", "que debe tener", 
        "qué necesita", "que necesita",
        "qué requisitos", "que requisitos",
        "requisitos del certificado", "requisitos de mi certificado",
        "qué debe incluir", "que debe incluir",
        "qué información debe", "que información debe",
        "cómo debe ser", "como debe ser",
        "qué datos debe", "que datos debe",
        "qué contiene", "que contiene"
    ]
    
    if any(info in q for info in palabras_informativas):
        return json.dumps({
            "case": "pregunta_informativa",
            "required_doc": None,
            "follow_up": [],
            "note": "USAR_FAQ",
            "fecha_detectada": False
        })

    # 1) Escalamiento incondicional
    if detectar_escalamiento(q):
        return json.dumps({
            "case": "escalamiento_inmediato",
            "required_doc": None,
            "follow_up": [],
            "note": obtener_mensaje_escalamiento(),
            "fecha_detectada": tiene_fecha
        })

    # 2) Muerte de mascota (solo empático)
    if any(p in q for p in ["mascota", "perro", "gato"]) and "falle" in q:
        return json.dumps({
            "case": "calamidad_mascota",
            "required_doc": None,
            "follow_up": [],
            "note": "Lo siento mucho por tu mascota. Esta situación no está contemplada como justificación de inasistencia.",
            "fecha_detectada": tiene_fecha
        })

    # 3) Mapeo general
    mapping = [
        ("enfermedad",       ["enfermedad", "gripe", "fiebre", "dolor", "doctor", "médico", "estómago", "cabeza", "migraña", 
                              "odontología", "odontólogo", "odontologo", "dentista", "dental", 
                              "muela", "muelas", "diente", "dientes", "cordal", "cordales", "extracción dental", 
                              "cirugia dental", "endodoncia", "ortodoncia", "brackets", "covid", "coronavirus", "malestar",
                              "presion", "vomito", "infeccion", "malestar general", "dolor muscular", "dolor de cabeza", "dolor de estomago"
                              "fatiga", "gastroenteritis", "colicos", "resfriado", "cita medica", "alergia"]),
        ("calamidad",        ["duelo", "fallec", "muerte", "defunción"]),
        ("deportiva",        ["deport", "competencia", "torneo", "evento deportivo"]),
        ("viaje_trabajo",    ["viaje de trabajo", "business trip", "laboral"]),
        ("clases_virtuales", ["virtuales", "remoto", "zoom", "teams"]),
    ]

    for case, keys in mapping:
        if any(k in q for k in keys):
            # ——————————— ENFERMEDAD ———————————
            if case == "enfermedad":
                # enfermedades catastróficas
                if any(sev in q for sev in ["cáncer", "leucemia", "tumor"]):
                    return json.dumps({
                        "case": "enfermedad_catastrica",
                        "required_doc": None,
                        "follow_up": [],
                        "note": "--mentor--",
                        "fecha_detectada": tiene_fecha
                    })

                # Si el usuario CONFIRMA que tiene el certificado (no solo lo menciona)
                # Excluir preguntas informativas como "qué debe tener mi certificado"
                confirmaciones_certificado = ["si tengo", "sí tengo", "lo tengo", "ya tengo", "tengo el certificado", "tengo mi certificado"]
                if any(kw in q for kw in confirmaciones_certificado):
                    return json.dumps({
                        "case": case,
                        "required_doc": "Certificado médico",
                        "follow_up": [
                          "<p>Perfecto. Por favor, sube aquí en el chat el certificado médico como archivo adjunto (imagen o PDF).</p>"
                        ],
                        "fecha_detectada": tiene_fecha
                    })

                # Caso normal de enfermedad: siempre pedir que suba el certificado al chat
                pregunta = "<p>Para procesar tu justificación, por favor sube tu certificado médico aquí en el chat como archivo adjunto (imagen o PDF).</p>"

                return json.dumps({
                    "case": case,
                    "required_doc": "Certificado médico",
                    "follow_up": [pregunta],
                    "fecha_detectada": tiene_fecha,
                    "fecha_extraida": fecha_extraida
                })

            # ——————————— CALAMIDAD DOMÉSTICA ———————————
            if case == "calamidad":
                return json.dumps({
                    "case": case,
                    "required_doc": "Acta de defunción y copia de cédula del estudiante",
                    "follow_up": [],
                    "note": "--mentor--",
                    "fecha_detectada": tiene_fecha
                })

            # ——————————— DEPORTE ———————————
            if case == "deportiva":
                if not tiene_fecha:
                    pregunta = "¿En qué fecha se realizó el evento?"
                else:
                    pregunta = "¿Tienes el certificado oficial de participación? Si lo tienes, súbelo aquí en el chat."

                return json.dumps({
                    "case": case,
                    "required_doc": "Certificado oficial de participación deportiva",
                    "follow_up": [pregunta],
                    "fecha_detectada": tiene_fecha,
                    "fecha_extraida": fecha_extraida
                })

            # ——————————— VIAJE DE TRABAJO ———————————
            if case == "viaje_trabajo":
                return json.dumps({
                    "case": case,
                    "required_doc": None,
                    "follow_up": [],
                    "note": "Lo siento, las inasistencias por viaje de trabajo no son justificables.",
                    "fecha_detectada": tiene_fecha
                })

            # ——————————— CLASES VIRTUALES ———————————
            if case == "clases_virtuales":
                if not tiene_fecha:
                    pregunta = "¿Por qué no pudiste asistir presencialmente?"
                else:
                    pregunta = "¿Puedes enviarme el reporte médico o justificativo por este medio?"

                return json.dumps({
                    "case": case,
                    "required_doc": "Reporte médico o justificación por escrito",
                    "follow_up": [pregunta],
                    "fecha_detectada": tiene_fecha,
                    "fecha_extraida": fecha_extraida
                })

    # ——————————— CASO DESCONOCIDO ———————————
    return json.dumps({
        "case": "desconocido",
        "required_doc": None,
        "follow_up": [],
        "note": "",
        "fecha_detectada": tiene_fecha,
        "fecha_extraida": fecha_extraida
    })


inquirer_agent = Agent(
    name="InquirerAgent",
    instructions="""
Dada una consulta sobre cómo justificar una falta, invoca la función `classify_justification_case`
y devuelve únicamente el JSON que ésta retorne.
- Si `note: "--mentor--"`, responde solo `--mentor--`.
- `follow_up` siempre tendrá **una sola pregunta** para continuar el diálogo.
""",
    tools=[classify_justification_case],
)
