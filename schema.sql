-- Ejecutar una vez en MySQL Workbench. No borra tablas existentes.
CREATE DATABASE IF NOT EXISTS uno_clase CHARACTER SET utf8mb4;
USE uno_clase;

CREATE TABLE IF NOT EXISTS usuarios (
    id INT PRIMARY KEY AUTO_INCREMENT,
    nombre VARCHAR(50) NOT NULL UNIQUE
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS partidas (
    id INT PRIMARY KEY AUTO_INCREMENT,
    estado ENUM('en_curso','terminada') NOT NULL DEFAULT 'en_curso',
    creada_en TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    terminada_en TIMESTAMP NULL,
    ganador_id INT NULL,
    FOREIGN KEY (ganador_id) REFERENCES usuarios(id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS participantes (
    partida_id INT NOT NULL,
    usuario_id INT NOT NULL,
    asiento INT NOT NULL,
    PRIMARY KEY (partida_id, usuario_id),
    UNIQUE (partida_id, asiento),
    FOREIGN KEY (partida_id) REFERENCES partidas(id),
    FOREIGN KEY (usuario_id) REFERENCES usuarios(id)
) ENGINE=InnoDB;

-- Una fila por carta FISICA de cada partida; las copias tienen distintos IDs.
CREATE TABLE IF NOT EXISTS cartas_partida (
    partida_id INT NOT NULL,
    carta_id INT NOT NULL,
    color VARCHAR(10) NOT NULL,
    valor VARCHAR(12) NOT NULL,
    zona ENUM('mazo','mano','descarte') NOT NULL,
    propietario_id INT NULL,
    orden INT NOT NULL,
    PRIMARY KEY (partida_id, carta_id),
    FOREIGN KEY (partida_id) REFERENCES partidas(id),
    FOREIGN KEY (partida_id, propietario_id)
        REFERENCES participantes(partida_id, usuario_id),
    INDEX (partida_id, zona, orden)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS jugadas (
    id INT PRIMARY KEY AUTO_INCREMENT,
    partida_id INT NOT NULL,
    usuario_id INT NULL,
    accion ENUM('inicio','robar','jugar','finalizar') NOT NULL,
    carta_id INT NULL,
    creada_en TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (partida_id) REFERENCES partidas(id),
    FOREIGN KEY (partida_id, usuario_id)
        REFERENCES participantes(partida_id, usuario_id),
    FOREIGN KEY (partida_id, carta_id)
        REFERENCES cartas_partida(partida_id, carta_id)
) ENGINE=InnoDB;
