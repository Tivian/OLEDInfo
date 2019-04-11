#include "i2c_master.hpp"

namespace I2C {
namespace {
    inline void start_condi() { TWCR = _BV(TWINT) | _BV(TWSTA) | _BV(TWEN);  }
    inline void stop_condi()  { TWCR = _BV(TWINT) | _BV(TWEN)  | _BV(TWSTO); }
    inline void ack_condi()   { TWCR = _BV(TWINT) | _BV(TWEN)  | _BV(TWEA);  }
    inline void nack_condi()  { TWCR = _BV(TWINT) | _BV(TWEN); }
    inline void send_data()   { TWCR = _BV(TWINT) | _BV(TWEN); }
    inline void set_data(uint8_t data) { TWDR = data; }
    inline uint8_t get_data() { return TWDR; }
    inline void reset() { TWCR = 0; }
}

bool init(uint8_t twbr, uint8_t twsr) {
    if (TWBR != 0)
        return false;

    TWBR = twbr;
    TWSR |= twsr;

    return true;
}

uint8_t start(uint8_t address, mode_t mode) {
    if (TWBR == 0)
        return 0;

    reset();
    start_condi();
    wait();
    
    uint8_t ret = status();
    if ((ret != TW_START) && (ret != TW_REP_START))
        return ret;

	ret = write((address << 1) | static_cast<uint8_t>(mode));
    return ((ret != TW_MT_SLA_ACK) && (ret != TW_MR_SLA_ACK)) ? ret : 0;
}

uint8_t write(uint8_t data) {
    if (TWBR == 0)
        return 0;

    set_data(data);
    send_data();
    wait();

    uint8_t ret = status();
    return ((ret != TW_MT_DATA_ACK) && (ret != TW_MR_DATA_ACK)) ? ret : 0;
}

uint8_t read_ack() {
    if (TWBR == 0)
        return 0;

    ack_condi();
    wait();

    return get_data();
}

uint8_t read_nack() {
    if (TWBR == 0)
        return 0;

    nack_condi();
    wait();

    return get_data();
}

inline uint8_t status() {
    return TWSR & I2C::STATUS_MASK;
}

void wait() {
    //loop_until_bit_is_set(TWCR, TWINT);
    uint16_t timeout = 0;
    while (!(TWCR & _BV(TWINT)) && !(timeout & 0x8000))
        timeout++;
}

void stop() {
    stop_condi();
}
}
