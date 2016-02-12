DIRS = Sim Proc Mem MemCtrl MemReq MemSched gzip MemWBMode
SRC = $(foreach dir, $(DIRS), $(shell find $(dir) -name '*.cs' -not -path '*Obsolete*'))
OBJ = sim.exe

.PHONY: all
all: $(OBJ)

$(OBJ): $(SRC)
	gmcs -debug -r:Mono.Posix -d:DEBUG -unsafe -out:$@ $^

.PHONY: clean
clean:
	rm -rf $(OBJ) $(OBJ).mdb $(shell find . -name '*~' -o -name '*.pyc')

.PHONY: run
run: $(OBJ)
	(cd bin/; mono --debug sim.exe config.txt)

.PHONY: arch
arch:
	tar zcvf code.tar.gz --exclude 'Common/gzip/*' Proc/ Net/ Common/
