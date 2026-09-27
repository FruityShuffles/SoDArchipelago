import json
import pkgutil

# Loaded via pkgutil so it works when the world is packaged as a zipped .apworld.
GAME_DATA = json.loads(pkgutil.get_data(__name__, "game_data.json").decode("utf-8"))
