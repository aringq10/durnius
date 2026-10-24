# Durnius

ASP.NET web backend for playing lithuanian card games.

`src/Durnius.Client/` contains a React frontend client.

Developed as a project for the Software system Engineering class in Vilnius
University.

## Running

Docker and Docker Compose are required. See `docker/`, `docker-compose.yml` and
`docker-compose.dev.yml` for more info.

``` bash
# production
docker compose -f docker-compose.yml up --build
# development
docker compose -f docker-compose.yml -f docker-compose.dev.yml up --build
```

Or use the bash script:

``` bash
# production
./dev prod up --build
# development
./dev up --build
```
