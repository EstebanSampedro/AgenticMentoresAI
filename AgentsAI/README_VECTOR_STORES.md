# 📚 Guía de Prueba Rápida: Subida de Archivos a OpenAI Vector Stores

Script simple para probar la subida de archivos a Vector Stores de OpenAI.

## 📁 Archivo

**`test_vector_store_upload.py`** - Script único con 3 funciones básicas

---

## 🚀 Configuración Rápida

### 1. Obtener tus credenciales de OpenAI

1. Ve a https://platform.openai.com/api-keys
2. Crea una API key nueva
3. Copia la key (empieza con `sk-proj-...`)

### 2. Obtener tu Vector Store ID

Opción A: Usando la plataforma web
1. Ve a https://platform.openai.com/storage/vector_stores
2. Selecciona tu vector store
3. Copia el ID (empieza con `vs_...`)

Opción B: Crear uno nuevo con código (ver ejemplo 7 en `ejemplos_vector_store.py`)

### 3. Configurar variables de entorno

**Windows PowerShell:**
```powershell
$env:OPENAI_API_KEY="sk-proj-TU_API_KEY_AQUI"
$env:VECTOR_STORE_ID="vs_TU_VECTOR_STORE_ID_AQUI"
```

**Linux/Mac:**
```bash
export OPENAI_API_KEY="sk-proj-TU_API_KEY_AQUI"
export VECTOR_STORE_ID="vs_TU_VECTOR_STORE_ID_AQUI"
```

### 4. Instalar dependencias

```bash
pip install openai fastapi python-multipart uvicorn requests
```

---

## 🧪 Uso Rápido

**Edita el archivo y descomenta UNA de estas 3 opciones:**

```python
# OPCIÓN 1: Subir UN archivo
result = upload_file_to_vector_store(
    file_path="ruta/a/tu/documento.pdf",
    vector_store_id=VECTOR_STORE_ID
)

# OPCIÓN 2: Subir VARIOS archivos
files = ["doc1.pdf", "doc2.pdf", "doc3.pdf"]
results = upload_multiple_files(files, VECTOR_STORE_ID)

# OPCIÓN 3: Ver archivos que YA están subidos
files = list_vector_store_files(VECTOR_STORE_ID)
```

**Ejecutar:**
```bash
python test_vector_store_upload.py
```

---

## 📊 Flujo Completo de Subida

```
1. Usuario → Archivo PDF
2. API/Script → OpenAI Files API (subir archivo)
3. OpenAI → Devuelve file_id
4. API/Script → Vector Stores API (agregar file_id al vector store)
5. OpenAI → Procesa el archivo (chunking, embeddings)
6. API/Script → Consulta estado hasta "completed"
```

---

## 🔍 Estados de Procesamiento

- **`in_progress`** - El archivo se está procesando
- **`completed`** - Procesamiento exitoso, listo para usar
- **`failed`** - Error en el procesamiento
- **`cancelled`** - Procesamiento cancelado

---

## ⚠️ Limitaciones y Consideraciones

### Límites de OpenAI:
- **Tamaño máximo por archivo**: 512 MB
- **Formatos soportados**: PDF, TXT, DOCX, PPTX, etc.
- **Atributos por objeto**: Máximo 16 pares key-value
- **Longitud de key**: 64 caracteres
- **Longitud de value**: 512 caracteres

### Mejores Prácticas:
1. ✅ Valida el formato del archivo antes de subir
2. ✅ Verifica el tamaño del archivo
3. ✅ Espera a que el estado sea "completed" antes de usar
4. ✅ Maneja errores de red y timeouts
5. ✅ Implementa reintentos para fallos transitorios

---

## 🔗 Integrando con tu Aplicación

### Opción 1: Endpoint en tu API existente

Agrega este endpoint a tu `AgentsAI/app/api/v1/endpoints/`:

```python
@router.post("/upload-knowledge-base/")
async def upload_to_knowledge_base(
    file: UploadFile = File(...),
    knowledge_base_type: str = Form(..., description="medical|university")
):
    """Sube un archivo al knowledge base correspondiente"""
    
    # Determinar vector store según tipo
    vector_store_map = {
        "medical": os.getenv("MEDICAL_VECTOR_STORE_ID"),
        "university": os.getenv("UNIVERSITY_VECTOR_STORE_ID")
    }
    
    vector_store_id = vector_store_map.get(knowledge_base_type)
    
    # Usar lógica del script de prueba
    # ...
```

### Opción 2: Servicio independiente

Usa `test_vector_store_api.py` como microservicio separado:
- Puerto diferente (ej: 8001)
- Deploy independiente
- Tu aplicación principal llama a este servicio

---

## 📝 Ejemplo de Uso en Producción

```python
from openai import OpenAI
import os

def upload_knowledge_document(file_path: str, category: str):
    """
    Sube un documento al knowledge base
    
    Args:
        file_path: Ruta al PDF
        category: 'medical' o 'university'
    """
    client = OpenAI(api_key=os.getenv("OPENAI_API_KEY"))
    
    # Mapeo de categorías a vector stores
    vector_stores = {
        "medical": "vs_medical_knowledge_123",
        "university": "vs_university_rules_456"
    }
    
    vector_store_id = vector_stores[category]
    
    # 1. Subir archivo
    with open(file_path, "rb") as file:
        file_obj = client.files.create(
            file=file,
            purpose="assistants"
        )
    
    # 2. Agregar al vector store
    vs_file = client.beta.vector_stores.files.create(
        vector_store_id=vector_store_id,
        file_id=file_obj.id
    )
    
    # 3. Esperar procesamiento (opcional)
    import time
---

## 🔗 Si funciona, puedes integrarlo a tu app

Copia las funciones de `test_vector_store_upload.py` a tu API:

```python
# En tu AgentsAI/app/api/v1/endpoints/
@router.post("/upload-knowledge-base/")
async def upload_to_knowledge_base(
    file: UploadFile = File(...),
    knowledge_base_type: str = Form(..., description="medical|university")
):
    # Copiar la lógica de upload_file_to_vector_store()
    # ...
```

---

## 📝 Ejemplo CompletoPDF

### Estado "failed" en procesamiento
- Revisa `last_error` en el objeto de estado
- El archivo puede estar corrupto o en formato no soportado

---

## 📚 Referencias

- [OpenAI Vector Stores API](https://platform.openai.com/docs/api-reference/vector-stores-files/createFile)
- [OpenAI Files API](https://platform.openai.com/docs/api-reference/files)
- [OpenAI Assistants Documentation](https://platform.openai.com/docs/assistants/overview)

---

## ✅ Checklist de Implementación

- [ ] Configurar `OPENAI_API_KEY`
- [ ] Obtener/crear `VECTOR_STORE_ID`
- [ ] Instalar dependencias
- [ ] Probar script CLI básico
- [ ] Probar API REST local
- [ ] Verificar que los archivos se procesen correctamente
- [ ] Integrar en aplicación principal
- [ ] Implementar manejo de errores
- [ ] Agregar logging
- [ ] Probar en producción
## ✅ Pasos para Probar

1. [ ] Configura `OPENAI_API_KEY` y `VECTOR_STORE_ID`
2. [ ] Instala: `pip install openai`
3. [ ] Edita `test_vector_store_upload.py` y descomenta una opción
4. [ ] Ejecuta: `python test_vector_store_upload.py`
5. [ ] Si funciona, intégralo a tu app principal