import express from "ultimate-express";
import Config from "./Utilities/Libraries/Config.js";
import {Console} from "./Utilities/Libraries/CS.js";
import PlayerController from "./Controllers/PlayerController.js";
import ImageController from "./Controllers/ImageController.js";
import CatalogController from "./Controllers/CatalogController.js";
import PlaceController from "./Controllers/PlaceController.js";

const App = express();
const ProcessPort = Config.Ports.Process;

App.use(express.json({ limit: "250mb" }));
App.use(express.text({ type: "text/*", limit: "250mb" }));

App.use((req, _, next) => {
    Console.Debug(`${req.method} ${req.path}`);
    next();
});

App.use("/player", PlayerController);
App.use("/image", ImageController);
App.use("/catalog", CatalogController);
App.use("/game", PlaceController);
App.get("/", (_, res) => {
    return res.status(200).send("RENDERER OK!");
});

App.listen(ProcessPort, () => Console.Log(`Renderer started on port &a&l${ProcessPort}`));
