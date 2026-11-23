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

## Test coverage

`./coverage` runs the tests with the Coverlet data collector and renders the
resulting Cobertura data as a browsable HTML report (ReportGenerator is pinned
as a local .NET tool in `dotnet-tools.json`):

``` bash
./coverage          # writes TestResults/CoverageReport/index.html
./coverage --open   # ...and opens it in the default browser
```
