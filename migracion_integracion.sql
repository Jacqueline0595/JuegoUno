-- Ejecutar UNA VEZ con tu cuenta administradora en MySQL Workbench.
-- Usa la misma base de db.py. No borra ni cambia las tablas anteriores.
USE uno_clase;
CREATE TABLE IF NOT EXISTS estado_juego (
    partida_id INT PRIMARY KEY,
    datos JSON NOT NULL,
    FOREIGN KEY (partida_id) REFERENCES partidas(id)
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS eventos_juego (
    id INT AUTO_INCREMENT PRIMARY KEY,
    partida_id INT NOT NULL,
    detalle JSON NOT NULL,
    creada_en TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (partida_id) REFERENCES partidas(id)
) ENGINE=InnoDB;
