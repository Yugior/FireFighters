# TC2008B Modelación de Sistemas Multiagentes con gráficas computacionales
# Python server to interact with Unity via POST
# Sergio Ruiz-Loza, Ph.D. March 2021

from http.server import BaseHTTPRequestHandler, HTTPServer
import logging
import json
import os
import sys

# Asegurar que el directorio del script esté en sys.path para permitir importar firefighters.py
script_dir = os.path.dirname(os.path.abspath(__file__))
if script_dir not in sys.path:
    sys.path.insert(0, script_dir)


from firefighters import Tablero, Bombero, BomberoAleatorio

# model = Tablero(width=8, height=6, num_agentes=6, fuego=4, humo=0, civiles=10, civiles_falsos=5, archivo_config='config.txt') 
model = Tablero(width=8, height=6, num_agentes=6, tipo_agente=Bombero, archivo_config='config.txt')

def to_unity(x, y):
        return {
            "x": float(x),
            "y": 0.0,
            "z": float(model.grid.height - 1 - y)
        }

class Server(BaseHTTPRequestHandler):
    
    def _set_response(self):
        self.send_response(200)
        self.send_header('Content-type', 'application/json')
        self.end_headers()
        
    def do_GET(self):
        self._set_response()
        self.wfile.write("GET request for {}".format(self.path).encode('utf-8'))

    


    def do_POST(self):
        global model

        # Leer cuerpo (aunque no lo usemos mucho)
        content_length = int(self.headers.get('Content-Length', 0))
        _ = self.rfile.read(content_length)

        try:
            # 1) Avanzar la simulación
            model.step_micro()

            # 2) Armar listas para Unity
            agents_data = []
            for idx, agent in enumerate(model.schedule):
                x, y = agent.pos
                pos = to_unity(x, y)
                agents_data.append({
                    "id": int(getattr(agent, "unique_id", idx)),
                    "x": pos["x"],
                    "y": pos["y"],
                    "z": pos["z"],
                    "has_civil": bool(getattr(agent, "tieneCivil", False))
                })

            fires_data = []
            for (x, y) in getattr(model, "posicionesFuego", []):
                fires_data.append(to_unity(x, y))

            smokes_data = []
            for (x, y) in getattr(model, "posicionesHumo", []):
                smokes_data.append(to_unity(x, y))

            bases_data = []
            for (x, y) in getattr(model, "base_position", []):
                bases_data.append(to_unity(x, y))

            # Civiles revelados (posiciones donde hay civil vivo conocido)
            civils_data = []
            for (x, y), revelado in getattr(model, "civiles_revelados", {}).items():
                if revelado:
                    civils_data.append(to_unity(x, y))

            # POIs (puntos de interés activos)
            pois_data = []
            for (x, y) in getattr(model, "posicionesPOI", []):
                pois_data.append(to_unity(x, y))

            # 3) Armar response CON ESTADÍSTICAS
            response = {
                "agents": agents_data,
                "fires": fires_data,
                "smokes": smokes_data,
                "bases": bases_data,
                "civils": civils_data,
                "pois": pois_data,

                "finished": getattr(model, "done", False),
                "outcome": getattr(model, "outcome", None),
                "end_reason": getattr(model, "end_reason", None),
                "end_code": getattr(model, "end_code", None),

                "steps": getattr(model, "steps", 0),
                "civiles_rescatados": getattr(model, "civiles_rescatados", 0),
                "civiles_perdidos": getattr(model, "civiles_perdidos", 0),
                "puntos_dano": getattr(model, "puntos_dano", 0),
                "fuegos_activos": len(getattr(model, "posicionesFuego", [])),
                "humos_activos": len(getattr(model, "posicionesHumo", [])),
                "pois_restantes": len(getattr(model, "posicionesPOI", [])),
            }

        except Exception as e:
            # Si algo truena, que no se caiga el server
            import traceback
            traceback.print_exc()
            logging.error(f"Error en do_POST: {e}")

            response = {
                "agents": [],
                "fires": [],
                "smokes": [],
                "bases": [],
                "civils": [],
                "pois": [],
                "finished": True,
                "outcome": "error",
                "end_reason": str(e),
                "end_code": "server_error",

                "steps": getattr(model, "steps", 0),
                "civiles_rescatados": getattr(model, "civiles_rescatados", 0),
                "civiles_perdidos": getattr(model, "civiles_perdidos", 0),
                "puntos_dano": getattr(model, "puntos_dano", 0),
                "fuegos_activos": len(getattr(model, "posicionesFuego", [])),
                "humos_activos": len(getattr(model, "posicionesHumo", [])),
                "pois_restantes": len(getattr(model, "posicionesPOI", [])),
            }

        # 4) Enviar siempre alguna respuesta
        self._set_response()
        self.wfile.write(json.dumps(response).encode('utf-8'))


def run(server_class=HTTPServer, handler_class=Server, port=8585):
    logging.basicConfig(level=logging.INFO)
    server_address = ('', port)
    httpd = server_class(server_address, handler_class)
    logging.info("Starting httpd...\n") # HTTPD is HTTP Daemon!
    try:
        httpd.serve_forever()
    except KeyboardInterrupt:   # CTRL+C stops the server
        pass
    httpd.server_close()
    logging.info("Stopping httpd...\n")

if __name__ == '__main__':
    from sys import argv
    
    if len(argv) == 2:
        run(port=int(argv[1]))
    else:
        run()



