"""
Script de prueba para subir archivos a OpenAI Vector Stores
Documentación: https://platform.openai.com/docs/api-reference/vector-stores-files/createFile
"""

import os
from openai import OpenAI
from pathlib import Path
from dotenv import load_dotenv

# Cargar variables de entorno desde .env
load_dotenv()

# Configuración
OPENAI_API_KEY = os.getenv("OPENAI_API_KEY")

# Validar que la API key está configurada
if not OPENAI_API_KEY:
    raise ValueError("❌ ERROR: OPENAI_API_KEY no está configurada. Verifica tu archivo .env")

# 👇 ELIGE UNO DE LOS DOS VECTOR STORES:
VECTOR_STORE_FAQ = os.getenv("OPENAI_VS_FAQ_ID")       # Para preguntas frecuentes
VECTOR_STORE_INQUIRER = os.getenv("OPENAI_VS_INQUIRER_ID")  # Para inquietudes

# 👇 SELECCIONA cuál quieres usar:
VECTOR_STORE_ID = VECTOR_STORE_FAQ  # 👈 CAMBIA ESTO a VECTOR_STORE_INQUIRER si quieres el otro

# Archivo de prueba a subir (cambia esta ruta a tu archivo)
# Formatos soportados: .pdf, .txt, .docx, .md, .json, .csv
TEST_FILE_PATH = "testing/docs/Mentores-Verdes-Modulo-AGENTES.pdf"


def upload_file_to_vector_store(file_path: str, vector_store_id: str):
    """
    Sube un archivo a un Vector Store de OpenAI
    
    Args:
        file_path: Ruta al archivo local
        vector_store_id: ID del vector store destino
    
    Returns:
        Respuesta de la API con información del archivo subido
    """
    try:
        # Inicializar cliente OpenAI
        client = OpenAI(api_key=OPENAI_API_KEY)
        
        print(f"📤 Subiendo archivo: {file_path}")
        print(f"🎯 Vector Store ID: {vector_store_id}")
        
        # Paso 1: Subir el archivo a OpenAI
        print("\n1️⃣ Subiendo archivo a OpenAI Files...")
        with open(file_path, "rb") as file:
            file_response = client.files.create(
                file=file,
                purpose="assistants"  # Propósito para usar con assistants/vector stores
            )
        
        file_id = file_response.id
        print(f"   ✅ Archivo subido con ID: {file_id}")
        print(f"   📊 Detalles: {file_response}")
        
        # Paso 2: Agregar el archivo al Vector Store
        print(f"\n2️⃣ Agregando archivo al Vector Store...")
        vector_store_file = client.vector_stores.files.create(
            vector_store_id=vector_store_id,
            file_id=file_id
        )
        
        print(f"   ✅ Archivo agregado al Vector Store")
        print(f"   📊 Detalles:")
        print(f"      - ID: {vector_store_file.id}")
        print(f"      - Object: {vector_store_file.object}")
        print(f"      - Status: {vector_store_file.status}")
        print(f"      - Vector Store ID: {vector_store_file.vector_store_id}")
        print(f"      - Created at: {vector_store_file.created_at}")
        
        # Paso 3: Verificar el estado
        print(f"\n3️⃣ Verificando estado del archivo en Vector Store...")
        file_status = client.vector_stores.files.retrieve(
            vector_store_id=vector_store_id,
            file_id=file_id
        )
        
        print(f"   📊 Estado actual: {file_status.status}")
        if file_status.status == "completed":
            print(f"   ✅ Procesamiento completado")
            print(f"   📈 Usage bytes: {file_status.usage_bytes if hasattr(file_status, 'usage_bytes') else 'N/A'}")
        elif file_status.status == "in_progress":
            print(f"   ⏳ Procesamiento en progreso...")
        elif file_status.status == "failed":
            print(f"   ❌ Error en procesamiento")
            if hasattr(file_status, 'last_error'):
                print(f"   ⚠️  Error: {file_status.last_error}")
        
        return {
            "file_id": file_id,
            "vector_store_file": vector_store_file,
            "status": file_status
        }
        
    except FileNotFoundError:
        print(f"❌ Error: El archivo '{file_path}' no existe")
        return None
    except Exception as e:
        print(f"❌ Error al subir archivo: {type(e).__name__}: {str(e)}")
        return None


def list_vector_store_files(vector_store_id: str):
    """
    Lista todos los archivos en un Vector Store
    
    Args:
        vector_store_id: ID del vector store
    """
    try:
        client = OpenAI(api_key=OPENAI_API_KEY)
        
        print(f"\n📋 Listando archivos en Vector Store: {vector_store_id}")
        
        files = client.vector_stores.files.list(
            vector_store_id=vector_store_id
        )
        
        print(f"\n   Total de archivos: {len(files.data)}")
        
        for idx, file in enumerate(files.data, 1):
            print(f"\n   {idx}. Archivo:")
            print(f"      - ID: {file.id}")
            print(f"      - Status: {file.status}")
            print(f"      - Created: {file.created_at}")
            if hasattr(file, 'usage_bytes'):
                print(f"      - Size: {file.usage_bytes} bytes")
        
        return files.data
        
    except Exception as e:
        print(f"❌ Error al listar archivos: {type(e).__name__}: {str(e)}")
        return None


def upload_multiple_files(file_paths: list, vector_store_id: str):
    """
    Sube múltiples archivos a un Vector Store
    
    Args:
        file_paths: Lista de rutas de archivos
        vector_store_id: ID del vector store destino
    """
    results = []
    
    print(f"\n🚀 Iniciando carga de {len(file_paths)} archivos...\n")
    
    for idx, file_path in enumerate(file_paths, 1):
        print(f"\n{'='*60}")
        print(f"Archivo {idx}/{len(file_paths)}")
        print(f"{'='*60}")
        
        result = upload_file_to_vector_store(file_path, vector_store_id)
        results.append({
            "file_path": file_path,
            "result": result,
            "success": result is not None
        })
    
    # Resumen
    print(f"\n\n{'='*60}")
    print(f"📊 RESUMEN DE CARGA")
    print(f"{'='*60}")
    
    successful = sum(1 for r in results if r["success"])
    failed = len(results) - successful
    
    print(f"✅ Exitosos: {successful}/{len(file_paths)}")
    print(f"❌ Fallidos: {failed}/{len(file_paths)}")
    
    if failed > 0:
        print(f"\n⚠️  Archivos fallidos:")
        for r in results:
            if not r["success"]:
                print(f"   - {r['file_path']}")
    
    return results


# ============================================================================
# EJEMPLO DE USO
# ============================================================================

if __name__ == "__main__":
    print("="*60)
    print("🧪 PRUEBA DE CARGA A OPENAI VECTOR STORE")
    print("="*60)
    
    # Verifica que tengas configuradas las variables
    if OPENAI_API_KEY == "tu-api-key-aqui":
        print("\n⚠️  ADVERTENCIA: Configura OPENAI_API_KEY")
        print("   Puedes usar: export OPENAI_API_KEY='sk-...'")
    
    if VECTOR_STORE_ID == "vs_abc123":
        print("\n⚠️  ADVERTENCIA: Configura VECTOR_STORE_ID")
        print("   Puedes usar: export VECTOR_STORE_ID='vs_...'")
    
    # Opción 1: Subir un solo archivo
    print("\n\n📝 OPCIÓN 1: Subir un archivo individual")
    print("-" * 60)
    
    # ⬇️ Subir el PDF de Mentores Verdes
    result = upload_file_to_vector_store(
        file_path="testing/docs/Mentores-Verdes-Modulo-AGENTES.pdf",
        vector_store_id=VECTOR_STORE_ID
    )
    
    # Opción 2: Subir múltiples archivos
    print("\n\n📝 OPCIÓN 2: Subir múltiples archivos")
    print("-" * 60)
    
    # Descomenta y modifica con tus archivos:
    # files_to_upload = [
    #     "documentos/manual1.pdf",
    #     "documentos/manual2.pdf",
    #     "documentos/politicas.pdf",
    # ]
    # results = upload_multiple_files(files_to_upload, VECTOR_STORE_ID)
    
    # Opción 3: Listar archivos existentes
    print("\n\n📝 OPCIÓN 3: Listar archivos en Vector Store")
    print("-" * 60)
    
    # Descomenta para listar:
    # files = list_vector_store_files(VECTOR_STORE_ID)
    
    print("\n\n✨ Script completado")
    print("\nℹ️  Para usar este script:")
    print("   1. Configura OPENAI_API_KEY y VECTOR_STORE_ID")
    print("   2. Descomenta una de las opciones (1, 2 o 3)")
    print("   3. Ejecuta: python test_vector_store_upload.py")
