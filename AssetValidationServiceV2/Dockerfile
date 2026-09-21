FROM golang:1.25-alpine AS build

WORKDIR /src

COPY go.mod go.sum ./
RUN go mod download

COPY . .
RUN CGO_ENABLED=0 go build -o /out/asset-validation .

FROM alpine:3.20

WORKDIR /app

COPY --from=build /out/asset-validation ./asset-validation

EXPOSE 4300

CMD ["./asset-validation"]
