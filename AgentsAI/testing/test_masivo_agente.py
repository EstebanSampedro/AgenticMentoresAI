"""
Script de Testing Masivo para el Agente de Mentores UDLA
=========================================================
Este script permite probar múltiples preguntas contra el agente
y registrar las respuestas en un archivo Excel.

Uso:
    1. Abre el archivo 'Preguntas Fuera del Alcance Mentor IA.xlsx'
    2. Ejecuta: python test_masivo_agente.py
    3. El mismo archivo se actualizará con las respuestas en "Resultado Real"
"""

import asyncio
import httpx
import pandas as pd
from datetime import datetime
from pathlib import Path
import time
import re
import os
from dotenv import load_dotenv

# Cargar variables de entorno desde el .env del directorio padre (AgentsAI/)
env_path = Path(__file__).parent.parent / ".env"
load_dotenv(env_path)

# ============================================================================
# CONFIGURACIÓN
# ============================================================================

# URL de tu API (ajusta el puerto si es diferente)
API_BASE_URL = os.getenv("API_BASE_URL", "http://localhost:8000")
API_ENDPOINT = f"{API_BASE_URL}/api/v1/agents/agent/"

# Token de autenticación - PEGA TU TOKEN AQUÍ
AUTH_TOKEN = os.getenv("AUTH_TOKEN", "")

# ⬇️ Si quieres pegar el token directamente, descomenta y pega aquí:
# AUTH_TOKEN = "eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiIs..."

# Archivo de entrada/salida (Excel con estructura: Temática, Pregunta, Resultado Esperado, Resultado Real)
INPUT_FILE = "CompletoPart1PreguntasIA.xlsx"

# Datos de usuario simulado para las pruebas
TEST_USER = {
    "fullName": "Estudiante Test",
    "nickname": "Test",
    "idCard": "1234567890",
    "career": "Ingeniería de Software",
    "email": "test@udla.edu.ec",
    "gender": "M"
}

# ============================================================================
# FUNCIONES
# ============================================================================

def limpiar_html(texto: str) -> str:
    """Remueve tags HTML para mejor lectura en Excel"""
    if not texto:
        return ""
    # Remueve tags HTML
    texto_limpio = re.sub(r'<[^>]+>', '', texto)
    # Remueve espacios múltiples
    texto_limpio = re.sub(r'\s+', ' ', texto_limpio).strip()
    return texto_limpio


def clasificar_resultado(respuesta: str) -> str:
    """Clasifica la respuesta del agente"""
    if not respuesta:
        return "ERROR"
    
    respuesta_lower = respuesta.lower()
    
    # Detectar escalamiento
    if "--mentor--" in respuesta_lower:
        return "ESCALÓ a mentor"
    
    # Detectar si rechazó la pregunta o dijo que no puede ayudar
    rechazos = [
        "no puedo ayudar",
        "no estoy capacitad",
        "fuera de mi alcance",
        "no tengo información",
        "no dispongo de esa información",
        "desconozco",
        "no es algo que pueda",
        "no me es posible",
        "no cuento con",
        "no tengo acceso",
        "no puedo proporcionar",
        "no estoy autoriza",
        "no me corresponde",
        "contacta con",
        "comunícate con",
        "bienestar estudiantil",
        "mariela.vaca"
    ]
    
    if any(r in respuesta_lower for r in rechazos):
        return "Rechazó/Redirigió correctamente"
    
    # Si respondió normalmente
    return "Respondió"


async def enviar_pregunta(client: httpx.AsyncClient, pregunta: str, session_id: str) -> dict:
    """Envía una pregunta al agente y retorna la respuesta"""
    
    headers = {
        "Content-Type": "application/json",
    }
    
    if AUTH_TOKEN:
        headers["Authorization"] = f"Bearer {AUTH_TOKEN}"
    
    payload = {
        "prompt": pregunta,
        "session_id": session_id,
        **TEST_USER
    }
    
    inicio = time.time()
    
    try:
        response = await client.post(
            API_ENDPOINT,
            json=payload,
            headers=headers,
            timeout=50.0  # Aumentado a 50 segundos
        )
        
        tiempo_respuesta = round(time.time() - inicio, 2)
        
        if response.status_code == 200:
            data = response.json()
            respuesta_agente = data.get("data", {}).get("response", "Sin respuesta")
            respuesta_limpia = limpiar_html(respuesta_agente)
            clasificacion = clasificar_resultado(respuesta_limpia)
            
            return {
                "success": True,
                "respuesta": respuesta_limpia,
                "clasificacion": clasificacion,
                "tiempo_seg": tiempo_respuesta,
                "error": None
            }
        else:
            return {
                "success": False,
                "respuesta": f"Error HTTP {response.status_code}",
                "clasificacion": "ERROR",
                "tiempo_seg": tiempo_respuesta,
                "error": response.text
            }
    
    except httpx.TimeoutException:
        return {
            "success": False,
            "respuesta": "Timeout - La API tardó demasiado",
            "clasificacion": "ERROR",
            "tiempo_seg": round(time.time() - inicio, 2),
            "error": "Timeout después de 120 segundos"
        }
    except httpx.ConnectError as e:
        return {
            "success": False,
            "respuesta": "Error de conexión - Servidor no disponible",
            "clasificacion": "ERROR",
            "tiempo_seg": round(time.time() - inicio, 2),
            "error": f"ConnectError: {str(e)}"
        }
    except Exception as e:
        return {
            "success": False,
            "respuesta": f"Error: {str(e)}",
            "clasificacion": "ERROR",
            "tiempo_seg": round(time.time() - inicio, 2),
            "error": str(e)
        }


async def ejecutar_tests(df: pd.DataFrame) -> pd.DataFrame:
    """Ejecuta todas las preguntas de prueba y actualiza el DataFrame"""
    
    total = len(df)
    resultados_reales = []
    tiempos = []
    clasificaciones = []
    
    async with httpx.AsyncClient() as client:
        for idx, row in df.iterrows():
            tematica = row.get("Temática", "")
            pregunta = row.get("Pregunta", "")
            resultado_esperado = row.get("Resultado Esperado", "")
            
            # Session ID único para cada pregunta (no mantener contexto entre preguntas)
            session_id = f"test_{idx}_{int(time.time())}"
            
            print(f"\n[{idx + 1}/{total}] [{tematica}]")
            print(f"    Pregunta: {pregunta[:60]}...")
            
            resultado = await enviar_pregunta(client, pregunta, session_id)
            
            resultados_reales.append(resultado["respuesta"])
            tiempos.append(resultado["tiempo_seg"])
            clasificaciones.append(resultado["clasificacion"])
            
            if resultado["success"]:
                print(f"    ✓ {resultado['clasificacion']} ({resultado['tiempo_seg']}s)")
                print(f"    Respuesta: {resultado['respuesta'][:80]}...")
            else:
                print(f"    ✗ Error: {resultado['error']}")
            
            # Pequeña pausa para no sobrecargar la API
            await asyncio.sleep(0.5)
    
    # Actualizar DataFrame
    df["Resultado Real"] = resultados_reales
    df["Clasificación"] = clasificaciones
    df["Tiempo (seg)"] = tiempos
    
    return df


def cargar_excel() -> pd.DataFrame:
    """Carga el archivo Excel"""
    if not Path(INPUT_FILE).exists():
        print(f"❌ Error: No se encontró el archivo '{INPUT_FILE}'")
        print(f"   Asegúrate de que el archivo está en la carpeta 'testing'")
        return None
    
    df = pd.read_excel(INPUT_FILE)
    print(f"✓ Cargadas {len(df)} preguntas desde '{INPUT_FILE}'")
    
    # Verificar columnas requeridas
    columnas_requeridas = ["Temática", "Pregunta"]
    for col in columnas_requeridas:
        if col not in df.columns:
            print(f"❌ Error: Falta la columna '{col}' en el Excel")
            return None
    
    return df


def guardar_excel(df: pd.DataFrame):
    """Guarda los resultados en un nuevo archivo Excel"""
    
    # Crear nombre de archivo con timestamp
    timestamp = datetime.now().strftime('%Y%m%d_%H%M%S')
    output_file = f"Resultados_Testing_{timestamp}.xlsx"
    
    # Guardar con formato
    with pd.ExcelWriter(output_file, engine='openpyxl') as writer:
        df.to_excel(writer, sheet_name='Resultados', index=False)
        
        # Ajustar anchos de columna
        worksheet = writer.sheets['Resultados']
        
        column_widths = {
            'A': 25,  # Temática
            'B': 60,  # Pregunta
            'C': 20,  # Resultado Esperado
            'D': 80,  # Resultado Real
            'E': 25,  # Clasificación
            'F': 12,  # Tiempo
        }
        
        for col, width in column_widths.items():
            worksheet.column_dimensions[col].width = width
    
    print(f"\n✓ Resultados guardados en: {output_file}")
    return output_file


def mostrar_resumen(df: pd.DataFrame):
    """Muestra un resumen del testing"""
    
    print(f"\n{'='*60}")
    print("📊 RESUMEN DE TESTING")
    print(f"{'='*60}")
    
    total = len(df)
    
    # Conteo por clasificación
    clasificaciones = df["Clasificación"].value_counts()
    print(f"\nResultados por clasificación:")
    for clasificacion, count in clasificaciones.items():
        porcentaje = count / total * 100
        print(f"  • {clasificacion}: {count} ({porcentaje:.1f}%)")
    
    # Conteo por temática
    print(f"\nResultados por temática:")
    for tematica in df["Temática"].unique():
        df_tematica = df[df["Temática"] == tematica]
        escalados = len(df_tematica[df_tematica["Clasificación"] == "ESCALÓ a mentor"])
        rechazados = len(df_tematica[df_tematica["Clasificación"] == "Rechazó/Redirigió correctamente"])
        respondidos = len(df_tematica[df_tematica["Clasificación"] == "Respondió"])
        print(f"  [{tematica}]")
        print(f"    - Escaló: {escalados}, Rechazó: {rechazados}, Respondió: {respondidos}")
    
    # Tiempo promedio
    tiempo_promedio = df["Tiempo (seg)"].mean()
    print(f"\n⏱ Tiempo promedio de respuesta: {tiempo_promedio:.2f} segundos")
    
    print(f"{'='*60}")


async def main():
    """Función principal"""
    print("="*60)
    print("🧪 TESTING MASIVO - AGENTE MENTORES UDLA")
    print("   Preguntas Fuera del Alcance")
    print("="*60)
    print(f"API Endpoint: {API_ENDPOINT}")
    print(f"Archivo: {INPUT_FILE}")
    print("="*60)
    
    # Cargar Excel
    df = cargar_excel()
    
    if df is None:
        return
    
    print(f"\n🚀 Iniciando testing de {len(df)} casos...\n")
    
    # Ejecutar tests
    df_resultados = await ejecutar_tests(df)
    
    # Guardar resultados
    guardar_excel(df_resultados)
    
    # Mostrar resumen
    mostrar_resumen(df_resultados)


if __name__ == "__main__":
    # Cambiar al directorio del script
    os.chdir(Path(__file__).parent)
    
    # Ejecutar
    asyncio.run(main())

