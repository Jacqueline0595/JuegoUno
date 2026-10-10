"""Una conexión por operación; confirmar todo o deshacer todo."""
import os
from contextlib import contextmanager

import mysql.connector
from fastapi import HTTPException


@contextmanager
def base_de_datos():
    conexion = None
    cursor = None
    try:
        conexion = mysql.connector.connect(
            host=os.getenv("UNO_DB_HOST", "127.0.0.1"),
            port=int(os.getenv("UNO_DB_PORT", "3306")),
            user=os.getenv("UNO_DB_USER", "uno_app"),
            password=os.getenv("UNO_DB_PASSWORD", ""),
            database=os.getenv("UNO_DB_NAME", "uno_clase"),
            charset="utf8mb4",
            connection_timeout=5,
        )
        cursor = conexion.cursor(dictionary=True)
        yield cursor
        conexion.commit()
    except mysql.connector.IntegrityError as error:
        if conexion:
            conexion.rollback()
        raise HTTPException(409, "Dato duplicado o referencia inválida.") from error
    except mysql.connector.Error as error:
        print(f"MySQL [{error.errno}]: {error.msg}", flush=True)
        if conexion:
            conexion.rollback()
        raise HTTPException(503, "No se pudo completar la operación en MySQL. Revisa configuración y esquema.") from error
    except Exception:
        if conexion:
            conexion.rollback()
        raise
    finally:
        if cursor:
            cursor.close()
        if conexion:
            conexion.close()
