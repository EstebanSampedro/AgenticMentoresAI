import re
from datetime import datetime, timedelta

def detectar_fecha_en_texto(texto: str) -> bool:
    """
    Detecta si el texto contiene una fecha explícita o referencia temporal.
    Retorna True si encuentra una fecha, False si no.
    """
    texto_lower = texto.lower()
    
    # Patrones de fechas explícitas
    patrones_fecha = [
        r'\b\d{1,2}[/-]\d{1,2}[/-]\d{2,4}\b',  # DD/MM/YYYY o DD-MM-YYYY
        r'\b\d{1,2}\s+de\s+\w+\s+de\s+\d{4}\b',  # 5 de enero de 2024
        r'\b\d{1,2}\s+de\s+\w+\b',  # 5 de enero
        r'\b\w+\s+\d{1,2}[,]?\s+\d{4}\b',  # enero 5, 2024
    ]
    
    for patron in patrones_fecha:
        if re.search(patron, texto):
            return True
    
    # Referencias temporales relativas
    referencias_temporales = [
        'ayer', 'hoy', 'mañana', 'anteayer', 'pasado mañana',
        'la semana pasada', 'esta semana', 'la próxima semana',
        'el lunes', 'el martes', 'el miércoles', 'el jueves', 'el viernes', 'el sábado', 'el domingo',
        'lunes pasado', 'martes pasado', 'miércoles pasado', 'jueves pasado', 'viernes pasado',
        'el día', 'ese día', 'aquel día', 'hace unos días', 'hace una semana',
        'la fecha fue', 'fue el día', 'ocurrió el'
    ]
    
    for ref in referencias_temporales:
        if ref in texto_lower:
            return True
    
    # Meses en español
    meses = [
        'enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio',
        'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre'
    ]
    
    for mes in meses:
        if mes in texto_lower:
            return True
    
    return False


def extraer_fecha_aproximada(texto: str) -> str | None:
    """
    Intenta extraer una fecha del texto y devolverla en formato legible.
    Retorna None si no encuentra una fecha clara.
    """
    texto_lower = texto.lower()
    
    # Buscar fechas explícitas DD/MM/YYYY
    patron_fecha = r'\b(\d{1,2})[/-](\d{1,2})[/-](\d{2,4})\b'
    match = re.search(patron_fecha, texto)
    if match:
        dia, mes, año = match.groups()
        if len(año) == 2:
            año = f"20{año}"
        return f"{dia}/{mes}/{año}"
    
    # Referencias temporales simples
    if 'ayer' in texto_lower:
        fecha_ayer = datetime.now() - timedelta(days=1)
        return fecha_ayer.strftime("%d/%m/%Y")
    
    if 'hoy' in texto_lower:
        return datetime.now().strftime("%d/%m/%Y")
    
    if 'mañana' in texto_lower and 'pasado mañana' not in texto_lower:
        fecha_manana = datetime.now() + timedelta(days=1)
        return fecha_manana.strftime("%d/%m/%Y")
    
    return None
