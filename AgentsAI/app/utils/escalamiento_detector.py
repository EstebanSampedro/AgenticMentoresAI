def detectar_escalamiento(query: str) -> bool:
    """
    Detecta si la consulta requiere escalamiento inmediato a mentor. SOLO RESPONDER --mentor--
    """
    q = query.lower()
    
    # Palabras que requieren escalamiento inmediato
    palabras_escalamiento = [
        'hospital', 'depresión', 'deprimido', 'deprimida', 'deprimidos', 'ansiedad', 'carcel', 'prisión', 'prision', 'robo', 'asalto',
        'cáncer', 'leucemia', 'tumor', 'quimioterapia', 'cirugía mayor', 'cirugia', 'cirugía',
        'accidente grave', 'accidente', 'accidente de tránsito', 'accidente vehicular', 'choque', 'colisión',
        'atropello', 'accidente de auto', 'accidente de carro', 'accidente de transito',
        'emergencia familiar', 'muerte', 'fallecimiento', 'fallecio', 'falleció', 'fallece', 'fallecera', 'fallecieron', 'murio', 'murió', 'muerieron', 'moriran',
        'defunción', 'funeral', 'sepelio', 'velorio', 'depresion', 'esclerosis', 'congreso', 'Congreso', 'congresos',
        'lupus', 'esquizofrenia', 'epilepsia', 'VIH', 'sida', 'transplante', 'anorexia', 'bulimia',
        'distrofia', 'suicidio', 'suicidas', 'salud mental', 'insomnio', 'mataron', 'mato', 'mató', 'problema mental', 'robaron', 'asaltaron', 'moría',
        'chocaron', 'hirieron', 'arma', 'pistola', 'metralleta', 'escopeta', 'machete', 'cuchillo', 'navaja', 'evento', 'evento deportivo', 'deportivo', 'competencia', 'competir', 'vida', 'acoso', 'acosando', 'denunciar', 'miedo', 'sexual','sexo',
        'viviendo', 'discriminado', 'discriminacion', 'discriminación', 'racismo', 'racista', 'religion', 'religión', 'droga', 'drogas',
        'abuso', 'abusaron', 'abusaran', 'abusó', 'abusar', 'discapacidad', 'discapacitado', 'lesion', 'lesionado', 'paja', 'pajas',

        'soledad', 'solitario', 'sola', 'morir', 'morirme', 'suicidarme', 'suicidar', 'vivir', 'desaparezco', 'desaparecio', 'aislado', 'aislada',
        'golpear', 'pegar', 'pegaron', 'golpearon', 'vengarme', 'venganza', 'vengara', 'lastimaron', 'lastimo', 'lastima', 'lastimar',
        'lastimare', 'maltrato', 'maltratando', 'maltrataron', 'maltratan', 'peligro', 'violencia', 'violacion', 'violento',
        'abusador', 'abusadora', 'coercion', 'extorsionar', 'extorsionaron', 'extorsiono', 'extorsionada', 'extorsionado', 'tristeza', 'panico', 'lloro',
        'llorando', 'llorar', 'inservible', 'angustia', 'existencial', 'alcohol', 'licor', 'cerveza', 'switch', 'volando', 'internaron', 'intoxicado', 'intoxicada',
        'drogado', 'drogaron', 'fumando', 'fumar', 'control', 'palpitaciones', 'dengue', 'denge', 'anestesia', 'bronquitis', 'amigdalitis', 'asma', 'descompense', 'descompensé', 'no me siento con buen animo',
        'chikungunya', 'hospitalizado', 'hospitalizacion', 'hospitalizada', 'episodios', 'gastroenteritis', 'diabetes', 'hernia',
        'vesicula', 'operación', 'operacion', 'operar', 'operaron', 'embarazo', 'embarazada', 'embarazaron', 'prenatal', 'anticonceptivos', 'parto', 'cesarea',
        'aborto', 'abortar', 'paleativos', 'paliativos', 'trauma', 'cerebral', 'calamidad', 'calamidad', 'presionado', 'presionada', 'presionando', 'homofobia', 'fobia', 'sexismo', 'sexista',
        'no me he sentido bien', 'no me siento bien ultimamente', 'de mi hijo', 'de mi hija', 'de mi hijastro', 'de mis hijos', 'de mis hijas', 'autolitico', 'autolítico', 'me siento mal', 'me he sentido mal',
        'trastorno', 'mama esta enferma', 'mama enfermo', 'mama enferma', 'mama se enfermo', 'papa se enfermo', 'papa esta enfermo', 'padre esta enfermo', 'abuelo enfermo', 'paro nacional',
        'protestas','manifestaciones', 'manifestacion', 'manifestación', 'apagon', 'apagón', 'crisis energetica', 'crisis energética', 'temblor', 'sismo', 'terremoto', 'inundacion', 'fracture', 'fracturé', 'fracturo', 'fracturó',
        'fracturaron', 'fractura', 'esguince', 'esguinzo', 'desmayo', 'desmayé', 'desmayo', 'intoxicacion', 'intoxicación', 'insolacion', 'insolación', 'quemadura', 'quemaduras', 'prenatal', 'mi hijo', 'mi hija',
        'mi mama esta enferma', 'mi papa esta enfermo', 'mi padre esta enfermo', 'mi abuelo esta enfermo', 'mi abuela esta enferma', 'mi mamá esta enferma', 'quiero una beca', 'como obtengo una beca', 'necesito una beca', 'proceso beca',
        'rechazaron', 'no me validaron el certificado', 'no me aprobaron', 'rechazo', 'rechazó', 'no aprobacion', 'no aprobación', 'no me ayudaron', 'no me justificaron', 'certificado no valido', 'certificado no válido',
        'beca', 'becas', 'representacion estudiantil', 'representación estudiantil', 'representacion universitaria', 'representación universitaria', 'certificado deportivo', 'competencia', 'competencias', 'torneo', 'torneos',
        'muy personal', 'situacion personal', 'situación personal', 'problema personal', 'problema muy personal', 'problema familiar', 'situacion familiar', 'situación familiar', 'asistiendo a clases', 'asistio a clases', 'asistira', 'notas de',
        'fue a clases', 'estuvo en clases', 'falsifico', 'falsificó', 'falsificacion', 'falsificación', 'documento falso', 'falsifica', 'choco', 'choque', 'vagos', 'vago', 'hack', 'hackear', 'hackeado', 'hackeada', 'hacker',
        'excluido', 'excluida', 'exclusion', 'empujo', 'empujaron', 'alcol', 'borracho', 'borracha', 'borrachos', 'tomados', 'esta tomado', '911', 'polic   ia', 'paramedicos', 'ambulancia', 'plagio', 'plagiaron', 'plagió', 'teme por', 'muy triste',
        'estresado', 'estresada', 'estrés', 'mascota', 'mi perro', 'mi gato', 'veterinario', 'hospital veterinario', 'veterinaria', 'mi perrito', 'mi gatita', 'mi gatito', 'mi perrita', 'no tengo certificado', 'sin certificado', 'no cuento con certificado',
        'no presente certificado', 'no he podido conseguir el certificado', 'no me has ayudado', 'no me has apoyado', 'no me han apoyado', 'no me han ayudado', 'no he recibido ayuda', 'no recibi ayuda', 'no recibí ayuda',
        'no ayudas', 'no apoyas', 'quiero ayuda humana', 'hablar con humano', 'audiencia', 'audiencias', 'judicial', 'demanda', 'juicio', 'tribunal'
    ]
    return any(palabra in q for palabra in palabras_escalamiento)


def es_caso_no_escalable(query: str) -> bool:
    """
    Detecta casos específicos que NO deben escalarse.
    """
    q = query.lower()
    
    # Casos específicos que no se escalan
    casos_no_escalables = [
        ('mascota', ['falle', 'murio', 'murió', 'muerte']),
        ('cita', ['embajada', 'consulado']),
        ('boda', ['matrimonio', 'casamiento']),
        ('trabajo', ['laboral', 'empresa'])
    ]
    
    for caso, palabras in casos_no_escalables:
        if caso in q and any(palabra in q for palabra in palabras):
            return True
    
    return False


def obtener_mensaje_escalamiento() -> str:
    """
    Retorna el mensaje estándar para escalamiento.
    """
    return "--mentor--"
