"""Reglas locales, sin SQL ni interfaz. La API guarda el resultado atómicamente.

Variante: 108 cartas, inicio numérico, sin acumulación ni desafío de +4.
Tras robar se permite jugar solo esa carta o pasar. +4 exige no tener el color.
"""
import random

COLORES = ("rojo", "amarillo", "verde", "azul")


def crear_baraja():
    cartas = []
    for color in COLORES:
        for valor in ["0"] + 2 * ([str(n) for n in range(1, 10)] + ["salto", "reversa", "+2"]):
            cartas.append(dict(carta_id=len(cartas)+1, color=color, valor=valor))
    for valor in ("comodin", "+4"):
        for _ in range(4):
            cartas.append(dict(carta_id=len(cartas)+1, color="negro", valor=valor))
    return cartas


def nueva(jugadores):
    if not 2 <= len(jugadores) <= 10 or len(set(jugadores)) != len(jugadores):
        raise ValueError("Se necesitan de 2 a 10 jugadores distintos.")
    numericas = [c for c in crear_baraja() if c["valor"].isdigit()]
    while True:
        sorteo = random.sample(numericas, len(jugadores))
        valores = [int(c["valor"]) for c in sorteo]
        if valores.count(max(valores)) == 1:
            break
    repartidor = valores.index(max(valores))
    turno = (repartidor + 1) % len(jugadores)
    cartas = crear_baraja()
    random.shuffle(cartas)
    limite = len(jugadores) * 7
    inicial = next(i for i in range(limite, 108) if cartas[i]["valor"].isdigit())
    cartas[limite], cartas[inicial] = cartas[inicial], cartas[limite]
    for i, carta in enumerate(cartas):
        carta.update(orden=i, zona="mazo", propietario_id=None)
        if i < limite:
            carta.update(zona="mano", propietario_id=jugadores[(turno+i) % len(jugadores)])
        elif i == limite:
            carta["zona"] = "descarte"
    return dict(jugadores_ids=jugadores, turno=turno, direccion=1,
                color_activo=cartas[limite]["color"], repartidor=repartidor,
                sorteo=sorteo, robada_id=None, version=0, cartas=cartas,
                estado="en_curso", ganador_id=None)


def mano(e, usuario):
    return [c for c in e["cartas"] if c["zona"] == "mano" and c["propietario_id"] == usuario]


def cima(e):
    return max((c for c in e["cartas"] if c["zona"] == "descarte"), key=lambda c: c["orden"])


def avanzar(e, pasos=1):
    e["turno"] = (e["turno"] + pasos * e["direccion"]) % len(e["jugadores_ids"])
    e["robada_id"] = None


def extraer(e, usuario, cantidad, eventos):
    for _ in range(cantidad):
        mazo = [c for c in e["cartas"] if c["zona"] == "mazo"]
        if not mazo:
            superior = cima(e)
            mazo = [c for c in e["cartas"] if c["zona"] == "descarte" and c is not superior]
            random.shuffle(mazo)
            orden = max(c["orden"] for c in e["cartas"]) + 1
            for i, c in enumerate(mazo):
                c.update(zona="mazo", orden=orden+i)
        if not mazo:
            raise ValueError("No hay cartas disponibles para completar el robo.")
        carta = min(mazo, key=lambda c: c["orden"])
        carta.update(zona="mano", propietario_id=usuario)
        eventos.append(dict(accion="robar", usuario_id=usuario, carta_id=carta["carta_id"]))


def actuar(e, usuario, accion, carta_id=None, color=None):
    if e["estado"] != "en_curso":
        raise ValueError("Esta partida ya terminó.")
    if usuario != e["jugadores_ids"][e["turno"]]:
        raise ValueError("No es el turno de ese jugador.")
    eventos = []
    if accion == "robar":
        if e["robada_id"] is not None:
            raise ValueError("Ya robaste: juega esa carta o pulsa Pasar.")
        extraer(e, usuario, 1, eventos)
        e["robada_id"] = eventos[-1]["carta_id"]
    elif accion == "pasar":
        if e["robada_id"] is None:
            raise ValueError("Primero debes robar una carta.")
        eventos.append(dict(accion="pasar", usuario_id=usuario, carta_id=None))
        avanzar(e)
    elif accion == "jugar":
        carta = next((c for c in mano(e, usuario) if c["carta_id"] == carta_id), None)
        if carta is None:
            raise ValueError("La carta no pertenece a tu mano.")
        if e["robada_id"] is not None and e["robada_id"] != carta_id:
            raise ValueError("Después de robar solo puedes jugar la carta recién robada.")
        valor = carta["valor"]
        if carta["color"] == "negro":
            if color not in COLORES:
                raise ValueError("Selecciona el color del comodín.")
            if valor == "+4" and any(c["color"] == e["color_activo"] for c in mano(e, usuario)):
                raise ValueError("No puedes usar +4 teniendo una carta del color activo.")
        elif carta["color"] != e["color_activo"] and valor != cima(e)["valor"]:
            raise ValueError("La carta debe coincidir en color o valor.")
        carta.update(zona="descarte", propietario_id=None,
                     orden=max(c["orden"] for c in e["cartas"])+1)
        e["color_activo"] = color if carta["color"] == "negro" else carta["color"]
        eventos.append(dict(accion="jugar", usuario_id=usuario, carta_id=carta_id, color=e["color_activo"]))
        pasos = 1
        if valor == "reversa":
            e["direccion"] *= -1
            if len(e["jugadores_ids"]) == 2:
                pasos = 2
        if valor == "salto":
            pasos = 2
        if valor in ("+2", "+4"):
            victima = e["jugadores_ids"][(e["turno"]+e["direccion"]) % len(e["jugadores_ids"])]
            extraer(e, victima, 2 if valor == "+2" else 4, eventos)
            pasos = 2
        avanzar(e, pasos)
        if not mano(e, usuario):
            e.update(estado="terminada", ganador_id=usuario)
            eventos.append(dict(accion="finalizar", usuario_id=usuario, carta_id=None))
    else:
        raise ValueError("Acción desconocida.")
    e["version"] += 1
    return eventos
