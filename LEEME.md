# UNO: interfaz C# conectada con Python y MySQL

## Para arrancar esta versión

1. En MySQL Workbench, con una cuenta administradora, ejecuta `schema.sql` si todavía no tienes la base `uno_clase` del ejemplo.
2. **Ejecuta `migracion_integracion.sql`** en esa misma base. Añade `estado_juego` y `eventos_juego` sin borrar partidas ni usuarios. También es necesario para una instalación nueva. Si ya diste permisos `SELECT, INSERT, UPDATE ON uno_clase.*` a `uno_app`, esos permisos abarcan las tablas nuevas.
3. Abre PowerShell **en esta carpeta**, donde están `api.py` y `db.py`. Usa las credenciales de MySQL que configuraste:

```powershell
$env:UNO_DB_HOST = "127.0.0.1"
$env:UNO_DB_PORT = "3306"
$env:UNO_DB_NAME = "uno_clase"
$credencialUno = Get-Credential -UserName "uno_app" -Message "Credenciales de MySQL"
$env:UNO_DB_USER = $credencialUno.UserName
$env:UNO_DB_PASSWORD = $credencialUno.GetNetworkCredential().Password
.\.venv\Scripts\python.exe -m uvicorn api:app --reload --host 127.0.0.1 --port 8000
```

4. Abre `InterfazUno.slnx` en Visual Studio y ejecuta. La solución apunta a `InterfazUno/InterfazUno.csproj`. El cliente `UnoApi.cs` ya está incluido como archivo vinculado y las referencias necesarias ya están agregadas.
5. Pulsa Jugar. La interfaz reutiliza o crea los perfiles `Jugador 1` a `Jugador 4`, solicita una partida y muestra el sorteo y reparto enviados por la API.

Si vienes de una copia sin entorno virtual, antes del paso 3 ejecuta:

```powershell
py -m venv .venv
.\.venv\Scripts\python.exe -m pip install -r requirements.txt
```

Si aún no existe la cuenta MySQL, créala desde Workbench con una cuenta administradora, eligiendo tu propia contraseña:

```sql
CREATE USER 'uno_app'@'127.0.0.1' IDENTIFIED BY 'REEMPLAZA_POR_TU_CONTRASENA';
GRANT SELECT, INSERT, UPDATE ON uno_clase.* TO 'uno_app'@'127.0.0.1';
```

No se han aplicado estos scripts a tu servidor habitual desde la revisión. La contraseña nunca va en C# ni en una URL. Si tu base definitiva sigue siendo `uno_project`, sus tablas diferentes requieren adaptación; cambiar solamente `UNO_DB_NAME` no las hace compatibles con esta API.

## Cómo se juega esta versión

- Las cuatro manos quedan visibles para la práctica en una sola computadora. Solo se habilita la mano del jugador en turno.
- Pulsa una carta para jugarla. La API valida propietario, turno y coincidencia de color o valor.
- Pulsa el mazo para robar una carta. Después puedes jugar **solo esa carta** o pulsar **Pasar**.
- Al jugar un comodín se abre una ventana para elegir color. Cerrar esa ventana cancela la acción.
- Salto omite al siguiente jugador; reversa cambia dirección; `+2` y `+4` hacen robar al siguiente y saltan su turno. Con dos jugadores, reversa funciona como salto.
- El `+4` solo se permite si no tienes ninguna carta del color activo. No hay acumulación de penalizaciones ni desafío de `+4`.
- El mazo recicla descartes cuando se agota, conservando la carta superior. Si no existen cartas suficientes para una operación, se rechaza y se revierte completa.
- Al quedarse sin cartas, el ganador se registra automáticamente, junto con los robos que produzca la última carta especial.
- **Nueva partida** cierra la actual sin ganador si seguía en curso y crea otra. El historial anterior permanece.
- **Actualizar** consulta de nuevo el estado guardado; úsalo después de una pérdida de conexión. No repitas a ciegas un movimiento cuyo resultado no llegó.

Variante elegida: baraja de 108 cartas y carta inicial **numérica**. Se conserva el sorteo de repartidor, calculado por la API con cartas numéricas; los empates se resuelven antes de mostrar el sorteo definitivo. El sorteo no consume las cartas de la partida. Faltan la penalización por no declarar UNO, puntuación acumulada, selección personalizada de perfiles y una pantalla para retomar partidas anteriores. Es un juego local, sin autenticación ni ocultamiento de manos.

## Qué cambió

| Archivo | Responsabilidad |
|---|---|
| `InterfazUno/Form2.cs` | Dibuja las cartas devueltas por la API; animación de reparto, clics, cambio de color y actualización de estado. |
| `UnoApi.cs` | Hace peticiones HTTP y transforma JSON en objetos de C#. |
| `api.py` | Recibe parámetros, consulta MySQL, llama al motor y guarda cada operación en una transacción. |
| `motor.py` | Baraja, reparto y reglas. No depende de SQL ni Windows Forms; permite probar reglas por separado. |
| `db.py` | Conexión, confirmación y reversión de transacciones; muestra el número de error MySQL en la terminal. |
| `schema.sql` | Las cinco tablas originales del ejemplo. |
| `migracion_integracion.sql` | Tablas adicionales para turno, color, dirección, versión e historial detallado. |
| `test_motor.py` | Pruebas de reglas y partidas completas simuladas. |

Las imágenes originales ahora están en `InterfazUno/Cartas` y `InterfazUno/Recursos`. Visual Studio las copia a la salida al compilar en Debug o Release. Las rutas usan `Application.StartupPath`; ya no dependen del repositorio de `source/repos`. `JuegoUNO.slnx` y los archivos viejos de la raíz siguen ahí, pero no forman parte de `InterfazUno.slnx`.

Las manos contienen objetos `Carta` identificados por `carta_id`, no solamente nombres de imágenes. Dos cartas del mismo color y valor comparten imagen, pero tienen IDs distintos. El método `ArchivoImagen()` traduce, por ejemplo, `valor="salto", color="rojo"` a `bloqueo_rojo.png`.

## Cómo se comunican los programas

```text
Form2 → UnoApi.cs → HTTP → Uvicorn/FastAPI → motor.py + MySQL
Form2 ← objetos C# ← JSON ← estado confirmado de la partida
```

C# no ejecuta ni compila los archivos Python. Visual Studio ejecuta la interfaz; Uvicorn ejecuta la API por separado. Ambos pueden vivir en el mismo repositorio. Mantén la terminal de Uvicorn abierta mientras juegas.

El formulario usa `await` para esperar sin congelarse y deshabilita acciones durante una petición. Si falla una escritura, intenta consultar el estado antes de permitir continuar; nunca repite un POST automáticamente.

La API bloquea la fila de la partida durante cada modificación. Todas las cartas afectadas, el turno, el ganador y los eventos se guardan juntos mediante `commit()`. Si algo falla, `rollback()` evita guardar media jugada.

Cada estado incluye `version`. C# la envía al jugar, robar, pasar o abandonar. Si se repite una petición o se usa un estado viejo, la API responde 409 y pide actualizar. Esto evita que una respuesta perdida provoque un segundo robo al repetir exactamente la solicitud.

## URL, JSON y SQL, con ejemplos

Un nombre se codifica en C# así:

```csharp
string ruta = "usuarios?nombre=" + Uri.EscapeDataString("Ana López");
```

`?` inicia parámetros y `&` los separa. `EscapeDataString` protege espacios, acentos y símbolos. FastAPI recupera el valor original. No conviertes todo el archivo C# a URL; construyes una dirección con los valores necesarios.

Una consulta de estado usa GET:

```text
GET http://127.0.0.1:8000/partidas/1
```

Un movimiento usa POST. Los IDs y la versión deben ser los del estado real:

```text
POST http://127.0.0.1:8000/partidas/1/jugar?usuario_id=3&carta_id=27&version=8
```

Para comodines se añade `&color=verde`. **POST también puede llevar datos en la URL**, como en el ejercicio de clase; abrir la dirección en la barra del navegador envía GET y no ejecuta esa acción. Usa [la documentación interactiva](http://127.0.0.1:8000/docs) para probar POST.

En Python:

```python
cursor.execute("SELECT id,nombre FROM usuarios WHERE id=%s", (usuario_id,))
```

`%s` es un marcador SQL que recibe el valor por separado. No es parte de una URL y no debe sustituirse concatenando texto del usuario.

FastAPI convierte un `dict` o una lista a JSON automáticamente. En esta versión **sí usamos `json` explícitamente** para serializar los metadatos que guardamos en la columna JSON de MySQL (`json.dumps`) y leerlos de vuelta (`json.loads`). Las respuestas de las rutas continúan devolviendo objetos, no cadenas creadas con `json.dumps`.

## Probar desde /docs

1. `GET /usuarios` comprueba conexión y tablas. `GET /` solo comprueba la API.
2. Crea perfiles con `POST /usuarios` si aún no tienes; anota sus IDs.
3. `POST /partidas?jugadores=1&jugadores=2` crea y devuelve todo el estado. Sustituye los IDs.
4. Consulta `jugadores[turno].id`: ese es el usuario que debe actuar.
5. Copia la `version` del estado a cada POST. Después de cada movimiento usa la nueva versión devuelta.
6. Consulta `/partidas/ID/cartas?zona=mazo`, `/partidas/ID/jugadas` o `/partidas/ID/eventos` para revisar lo guardado. Eventos incluye sorteo, reparto inicial, colores, pases y abandonos; jugadas conserva el formato original de movimientos de cartas.

Cambios respecto a la primera guía: crear partida y los POST de juego ahora devuelven el estado completo; `jugar` y `robar` requieren versión; se añadieron `pasar`, `abandonar` y `eventos`. Se retiró `/finalizar`: el motor detecta al ganador al jugar la última carta. El nuevo `UnoApi.cs` ya usa este contrato.

Partidas creadas por la API anterior mantienen sus registros y pueden consultarse en listados, cartas y jugadas. No se les inventa un turno: la consulta del nuevo estado devuelve 409 y pide iniciar una nueva.

## Si aparece un error

- **503:** mira la terminal de Uvicorn. `db.py` imprime el código y mensaje original de MySQL. Si falta `estado_juego` o `eventos_juego`, ejecuta la migración.
- **1045 de MySQL:** revisa usuario, contraseña y host permitido.
- **1049:** no existe la base configurada.
- **1146:** falta una tabla o seleccionaste otra base.
- **409 de la API:** puede ser una jugada inválida, turno incorrecto o versión antigua. El texto indica cuál.
- **No se puede conectar:** mantén encendida la API en `127.0.0.1:8000` y configura las variables en la misma terminal donde la arrancas.

## Pruebas de reglas

Desde esta carpeta:

```powershell
.\.venv\Scripts\python.exe -m unittest test_motor -v
```

Las pruebas verifican cantidades e IDs, reparto, rechazo de cartas incompatibles, propiedad, turnos, salto, reversa, robos, comodines, reciclaje y final de partida. No necesitas MySQL para ejecutarlas.

La solución se verificó en Debug y Release. También se probó la API por HTTP con un servidor MySQL temporal aislado: partida completa, historial, ganador, rechazo de reintentos, reversión de operaciones inválidas y persistencia después de reiniciar la API. Esto no confirma tus credenciales ni aplica la migración en tu servidor habitual.

Se probó además el cliente C# real contra esa API: deserialización del estado, creación de partida, robo, pase, consulta, abandono y correspondencia de las 108 cartas con sus archivos de imagen. No se realizó una revisión visual de la ventana en ejecución.

El cliente usa `http://127.0.0.1:8000/`. Opcionalmente puedes configurar `UNO_API_URL` antes de iniciar Visual Studio para cambiarla; incluye la barra final. No es necesario para el funcionamiento habitual.
