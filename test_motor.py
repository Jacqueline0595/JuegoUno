import copy
import random
import unittest
from collections import Counter
from motor import crear_baraja, nueva, actuar, mano, cima


class Reglas(unittest.TestCase):
    def setUp(self):
        self.e = nueva([11, 22, 33, 44])
        self.e.update(turno=0, direccion=1, robada_id=None, color_activo="rojo")
        for i, c in enumerate(self.e["cartas"]):
            c.update(zona="mazo", propietario_id=None, orden=i)
        self.tomar("rojo", "5").update(zona="descarte")
        self.dar("azul", "9", 11)

    def tomar(self, color, valor):
        return next(c for c in self.e["cartas"] if c["zona"] == "mazo" and c["color"] == color and c["valor"] == valor)

    def dar(self, color, valor, usuario):
        c = self.tomar(color, valor)
        c.update(zona="mano", propietario_id=usuario)
        return c["carta_id"]

    def test_baraja_y_reparto(self):
        b = crear_baraja()
        self.assertEqual(len(b), 108)
        self.assertEqual(len({c["carta_id"] for c in b}), 108)
        cantidades = Counter((c["color"], c["valor"]) for c in b)
        self.assertEqual(cantidades["rojo", "0"], 1)
        self.assertEqual(cantidades["rojo", "5"], 2)
        self.assertEqual(cantidades["negro", "+4"], 4)
        for n in (2, 4, 10):
            e = nueva(list(range(n)))
            self.assertTrue(cima(e)["valor"].isdigit())
            self.assertTrue(all(len(mano(e, u)) == 7 for u in range(n)))
            self.assertEqual(sum(c["zona"] == "mazo" for c in e["cartas"]), 107-7*n)

    def test_turno_y_propiedad(self):
        with self.assertRaises(ValueError): actuar(self.e, 22, "robar")
        with self.assertRaises(ValueError): actuar(self.e, 11, "jugar", 999)

    def test_incompatible_no_modifica(self):
        antes = copy.deepcopy(self.e)
        with self.assertRaises(ValueError): actuar(self.e, 11, "jugar", mano(self.e, 11)[0]["carta_id"])
        self.assertEqual(self.e, antes)

    def test_coincidencia_valor(self):
        actuar(self.e, 11, "jugar", self.dar("azul", "5", 11))
        self.assertEqual(self.e["turno"], 1)
        self.assertEqual(self.e["color_activo"], "azul")

    def test_salto(self):
        actuar(self.e, 11, "jugar", self.dar("rojo", "salto", 11))
        self.assertEqual(self.e["turno"], 2)

    def test_reversa(self):
        actuar(self.e, 11, "jugar", self.dar("rojo", "reversa", 11))
        self.assertEqual(self.e["direccion"], -1)
        self.assertEqual(self.e["turno"], 3)

    def test_reversa_dos_jugadores(self):
        self.e["jugadores_ids"] = [11, 22]
        actuar(self.e, 11, "jugar", self.dar("rojo", "reversa", 11))
        self.assertEqual(self.e["turno"], 0)

    def test_mas_dos(self):
        eventos = actuar(self.e, 11, "jugar", self.dar("rojo", "+2", 11))
        self.assertEqual(len(mano(self.e, 22)), 2)
        self.assertEqual(sum(x["accion"] == "robar" for x in eventos), 2)
        self.assertEqual(self.e["turno"], 2)

    def test_comodin_color_obligatorio(self):
        c = self.dar("negro", "comodin", 11)
        with self.assertRaises(ValueError): actuar(self.e, 11, "jugar", c)
        actuar(self.e, 11, "jugar", c, "verde")
        self.assertEqual(self.e["color_activo"], "verde")

    def test_mas_cuatro_restringido(self):
        c = self.dar("negro", "+4", 11)
        self.dar("rojo", "3", 11)
        with self.assertRaises(ValueError): actuar(self.e, 11, "jugar", c, "azul")

    def test_mas_cuatro(self):
        actuar(self.e, 11, "jugar", self.dar("negro", "+4", 11), "verde")
        self.assertEqual(len(mano(self.e, 22)), 4)
        self.assertEqual(self.e["turno"], 2)
        self.assertEqual(self.e["color_activo"], "verde")

    def test_robar_y_pasar(self):
        with self.assertRaises(ValueError): actuar(self.e, 11, "pasar")
        anterior = self.dar("rojo", "2", 11)
        actuar(self.e, 11, "robar")
        self.assertIsNotNone(self.e["robada_id"])
        with self.assertRaises(ValueError): actuar(self.e, 11, "robar")
        with self.assertRaises(ValueError): actuar(self.e, 11, "jugar", anterior)
        actuar(self.e, 11, "pasar")
        self.assertEqual(self.e["turno"], 1)
        self.assertIsNone(self.e["robada_id"])

    def test_reciclaje_conserva_superior(self):
        for c in self.e["cartas"]:
            if c["zona"] == "mazo": c.update(zona="descarte")
        superior = cima(self.e)["carta_id"]
        actuar(self.e, 11, "robar")
        self.assertEqual(cima(self.e)["carta_id"], superior)
        self.assertEqual(len(self.e["cartas"]), 108)

    def test_ganador_automatico(self):
        self.e["color_activo"] = "azul"
        actuar(self.e, 11, "jugar", mano(self.e, 11)[0]["carta_id"])
        self.assertEqual(self.e["ganador_id"], 11)
        self.assertEqual(self.e["estado"], "terminada")
        with self.assertRaises(ValueError): actuar(self.e, 22, "robar")

    def test_partidas_completas_conservan_cartas(self):
        random.seed(72)
        for _ in range(8):
            e = nueva([11, 22, 33, 44])
            for turno in range(4000):
                if e["estado"] != "en_curso": break
                u = e["jugadores_ids"][e["turno"]]
                for c in mano(e, u):
                    intento = copy.deepcopy(e)
                    try: actuar(intento, u, "jugar", c["carta_id"], "rojo")
                    except ValueError: continue
                    e = intento
                    break
                else:
                    actuar(e, u, "pasar" if e["robada_id"] is not None else "robar")
                self.assertEqual(len({c["carta_id"] for c in e["cartas"]}), 108)
                self.assertTrue(all((c["propietario_id"] is not None) == (c["zona"] == "mano") for c in e["cartas"]))
            self.assertEqual(e["estado"], "terminada")


if __name__ == "__main__":
    unittest.main()
