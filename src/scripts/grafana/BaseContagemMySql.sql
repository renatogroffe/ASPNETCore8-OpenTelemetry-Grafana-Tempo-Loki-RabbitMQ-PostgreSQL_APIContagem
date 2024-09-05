CREATE TABLE HistoricoContagem (
    Id INT NOT NULL AUTO_INCREMENT,
    DataProcessamento TIMESTAMP NOT NULL,
    ValorAtual INT NOT NULL,
    Producer VARCHAR(120) NOT NULL,
    Kernel VARCHAR(80) NOT NULL,
    Framework VARCHAR(80) NOT NULL,
    Mensagem VARCHAR(500) NOT NULL,
    PRIMARY KEY (Id)
);