"""API local. Ejecutar schema.sql y migracion_integracion.sql primero."""
import json
from fastapi import FastAPI, HTTPException, Query
from db import base_de_datos
from motor import crear_baraja, nueva, actuar

app = FastAPI(title="UNO local", version="0.2.0")


def exigir_partida(c, id, bloquear=False):
    c.execute("SELECT * FROM partidas WHERE id=%s" + (" FOR UPDATE" if bloquear else ""), (id,))
    p = c.fetchone()
    if p is None:
        raise HTTPException(404, "La partida no existe.")
    return p


def leer(c, id, bloquear=False):
    p = exigir_partida(c, id, bloquear)
    c.execute("SELECT datos FROM estado_juego WHERE partida_id=%s", (id,))
    fila = c.fetchone()
    if fila is None:
        raise HTTPException(409, "Partida anterior a la integración. Crea una nueva; su historial se conserva.")
    e = json.loads(fila["datos"])
    e.update(p)
    c.execute("SELECT carta_id,color,valor,zona,propietario_id,orden FROM cartas_partida WHERE partida_id=%s ORDER BY orden", (id,))
    e["cartas"] = c.fetchall()
    c.execute("SELECT u.id,u.nombre FROM participantes p JOIN usuarios u ON u.id=p.usuario_id WHERE p.partida_id=%s ORDER BY p.asiento", (id,))
    e["jugadores"] = c.fetchall()
    return e


def guardar(c, e, eventos):
    c.executemany("UPDATE cartas_partida SET zona=%s,propietario_id=%s,orden=%s WHERE partida_id=%s AND carta_id=%s",
                  [(a["zona"], a["propietario_id"], a["orden"], e["id"], a["carta_id"]) for a in e["cartas"]])
    claves = ("jugadores_ids", "turno", "direccion", "color_activo", "repartidor", "sorteo", "robada_id", "version")
    datos = json.dumps({k: e[k] for k in claves}, ensure_ascii=False)
    c.execute("INSERT INTO estado_juego(partida_id,datos) VALUES (%s,%s) ON DUPLICATE KEY UPDATE datos=%s", (e["id"], datos, datos))
    c.execute("UPDATE partidas SET estado=%s,ganador_id=%s,terminada_en=IF(%s='terminada',CURRENT_TIMESTAMP,NULL) WHERE id=%s",
              (e["estado"], e["ganador_id"], e["estado"], e["id"]))
    for evento in eventos:
        c.execute("INSERT INTO eventos_juego(partida_id,detalle) VALUES (%s,%s)", (e["id"], json.dumps(evento, ensure_ascii=False)))
        if evento["accion"] in ("inicio", "robar", "jugar", "finalizar"):
            c.execute("INSERT INTO jugadas(partida_id,usuario_id,accion,carta_id) VALUES (%s,%s,%s,%s)",
                      (e["id"], evento.get("usuario_id"), evento["accion"], evento.get("carta_id")))


@app.get("/")
def inicio():
    return {"message": "API de UNO activa", "version": "0.2.0"}


@app.get("/hola")
def hola(ciudad: str = "otro", cine: int = 0):
    return {"message": ciudad, "theather": cine}


@app.get("/usuarios")
def listar_usuarios():
    with base_de_datos() as c:
        c.execute("SELECT id,nombre FROM usuarios ORDER BY id")
        return c.fetchall()


@app.post("/usuarios", status_code=201)
def crear_usuario(nombre: str = Query(..., min_length=1, max_length=50)):
    nombre = nombre.strip()
    if not nombre:
        raise HTTPException(422, "El nombre no puede estar vacío.")
    with base_de_datos() as c:
        c.execute("INSERT INTO usuarios(nombre) VALUES (%s)", (nombre,))
        return {"id": c.lastrowid, "nombre": nombre}


@app.post("/partidas", status_code=201)
def crear_partida(jugadores: list[int] = Query(...)):
    try:
        e = nueva(jugadores)
    except ValueError as error:
        raise HTTPException(422, str(error)) from error
    with base_de_datos() as c:
        for usuario in jugadores:
            c.execute("SELECT id FROM usuarios WHERE id=%s", (usuario,))
            if c.fetchone() is None:
                raise HTTPException(404, f"No existe el jugador {usuario}.")
        c.execute("INSERT INTO partidas () VALUES ()")
        e["id"] = c.lastrowid
        c.executemany("INSERT INTO participantes(partida_id,usuario_id,asiento) VALUES (%s,%s,%s)", [(e["id"], u, i) for i, u in enumerate(jugadores)])
        c.executemany("INSERT INTO cartas_partida(partida_id,carta_id,color,valor,zona,propietario_id,orden) VALUES (%s,%s,%s,%s,%s,%s,%s)",
                      [(e["id"], a["carta_id"], a["color"], a["valor"], a["zona"], a["propietario_id"], a["orden"]) for a in e["cartas"]])
        guardar(c, e, [dict(accion="inicio", sorteo=e["sorteo"], repartidor=e["repartidor"], reparto=e["cartas"])])
        return leer(c, e["id"])


@app.get("/partidas")
def listar_partidas(limite: int = Query(50, ge=1, le=200)):
    with base_de_datos() as c:
        c.execute("SELECT p.*,u.nombre AS ganador_nombre FROM partidas p LEFT JOIN usuarios u ON u.id=p.ganador_id ORDER BY p.id DESC LIMIT %s", (limite,))
        return c.fetchall()


@app.get("/partidas/{partida_id}")
def consultar_partida(partida_id: int):
    with base_de_datos() as c:
        return leer(c, partida_id)


@app.get("/partidas/{partida_id}/cartas")
def consultar_cartas(partida_id: int, zona: str = Query("mazo", pattern="^(mazo|mano|descarte)$"), usuario_id: int | None = None):
    with base_de_datos() as c:
        exigir_partida(c, partida_id)
        sql = "SELECT carta_id,color,valor,zona,propietario_id,orden FROM cartas_partida WHERE partida_id=%s AND zona=%s"
        params = [partida_id, zona]
        if usuario_id is not None:
            sql += " AND propietario_id=%s"
            params.append(usuario_id)
        c.execute(sql + " ORDER BY orden", tuple(params))
        cartas = c.fetchall()
        return dict(partida_id=partida_id, cantidad=len(cartas), cartas=cartas)


@app.get("/partidas/{partida_id}/jugadas")
def consultar_jugadas(partida_id: int):
    with base_de_datos() as c:
        exigir_partida(c, partida_id)
        c.execute("SELECT * FROM jugadas WHERE partida_id=%s ORDER BY id", (partida_id,))
        return c.fetchall()


@app.get("/partidas/{partida_id}/eventos")
def consultar_eventos(partida_id: int):
    with base_de_datos() as c:
        exigir_partida(c, partida_id)
        c.execute("SELECT * FROM eventos_juego WHERE partida_id=%s ORDER BY id", (partida_id,))
        eventos = c.fetchall()
        for evento in eventos:
            evento["detalle"] = json.loads(evento["detalle"])
        return eventos


def movimiento(partida_id, usuario_id, version, accion, carta_id=None, color=None):
    with base_de_datos() as c:
        e = leer(c, partida_id, bloquear=True)
        if version != e["version"]:
            raise HTTPException(409, "El estado cambió. Actualiza la partida antes de intentar otra acción.")
        try:
            eventos = actuar(e, usuario_id, accion, carta_id, color)
        except ValueError as error:
            raise HTTPException(409, str(error)) from error
        guardar(c, e, eventos)
        return leer(c, partida_id)


@app.post("/partidas/{partida_id}/robar")
def robar_carta(partida_id: int, usuario_id: int, version: int):
    return movimiento(partida_id, usuario_id, version, "robar")


@app.post("/partidas/{partida_id}/jugar")
def jugar_carta(partida_id: int, usuario_id: int, carta_id: int, version: int, color: str | None = None):
    return movimiento(partida_id, usuario_id, version, "jugar", carta_id, color)


@app.post("/partidas/{partida_id}/pasar")
def pasar(partida_id: int, usuario_id: int, version: int):
    return movimiento(partida_id, usuario_id, version, "pasar")


@app.post("/partidas/{partida_id}/abandonar")
def abandonar(partida_id: int, version: int):
    with base_de_datos() as c:
        e = leer(c, partida_id, bloquear=True)
        if e["version"] != version or e["estado"] != "en_curso":
            raise HTTPException(409, "Actualiza la partida antes de reiniciar.")
        e.update(estado="terminada", ganador_id=None, version=version+1)
        guardar(c, e, [dict(accion="abandonar")])
        return leer(c, partida_id)
